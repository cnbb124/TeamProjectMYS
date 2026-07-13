using System.Text.Json;
using MySqlConnector;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// appsettings.json 의 ConnectionStrings:MySql 을 읽음 (없으면 아래 기본값)
string connString = builder.Configuration.GetConnectionString("MySql")
    ?? "server=localhost;port=3306;user=root;password=;database=teamprojectmys";

// =====================================================================
// 1) 서버 자체가 살아있는지 확인용
// =====================================================================
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// =====================================================================
// 2) MySQL 에 실제로 붙는지 확인용 (users 테이블 개수 세보기)
// =====================================================================
app.MapGet("/db/ping", async () =>
{
    try
    {
        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand("SELECT COUNT(*) FROM users;", conn);
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        return Results.Ok(new { db = "connected", users = count });
    }
    catch (Exception ex)
    {
        return Results.Problem("DB 연결 실패: " + ex.Message);
    }
});

// =====================================================================
// 3) 회원가입: { "username": "...", "password": "..." } 를 받아 계정 생성
//    성공하면 새 userId 를 돌려줌. 이후 저장/로드는 이 번호로 함.
// =====================================================================
app.MapPost("/register", async (RegisterRequest req) =>
{
    // (1) 입력 검사 — 빈 값이면 거절
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "아이디/비밀번호를 입력하세요." });
    if (req.Username.Length > 50)
        return Results.BadRequest(new { error = "아이디는 50자 이하로 해주세요." });

    // (2) 비밀번호는 절대 원문 저장 금지 → BCrypt로 "다져서"(해시) 저장.
    //     해시는 일방통행이라 DB가 털려도 원래 비번을 알 수 없음.
    string hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

    try
    {
        await using var conn = new MySqlConnection(connString);
        await conn.OpenAsync();

        // (3) users 테이블에 계정 추가
        await using var cmd = new MySqlCommand(
            "INSERT INTO users (username, password_hash) VALUES (@u, @h);", conn);
        cmd.Parameters.AddWithValue("@u", req.Username);   // @u 자리에 안전하게 끼움 (SQL 인젝션 방지)
        cmd.Parameters.AddWithValue("@h", hash);
        await cmd.ExecuteNonQueryAsync();
        long userId = cmd.LastInsertedId;                  // 방금 발급된 고유 번호

        // ※ 세이브 슬롯은 여기서 만들지 않음 — 슬롯이 유저당 최대 10개(0~9)라
        //   어떤 슬롯을 쓸지 미리 알 수 없음. 그 슬롯에 처음 저장(POST /save)할 때
        //   자동으로 생성됨(UPSERT). 로드 화면은 GET /saves 로 목록을 받아서 그림.

        return Results.Ok(new { userId, username = req.Username });
    }
    catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
    {
        // username 컬럼이 UNIQUE 라서 중복이면 MySQL이 거절함 → 그걸 받아서 안내
        return Results.Conflict(new { error = "이미 사용 중인 아이디입니다." });
    }
});

// =====================================================================
// 4) 로그인: 아이디/비번 맞으면 userId 돌려줌
// =====================================================================
app.MapPost("/login", async (LoginRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
        return Results.BadRequest(new { error = "아이디/비밀번호를 입력하세요." });

    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    // 아이디로 계정 찾기
    await using var cmd = new MySqlCommand(
        "SELECT id, password_hash FROM users WHERE username = @u;", conn);
    cmd.Parameters.AddWithValue("@u", req.Username);

    await using var reader = await cmd.ExecuteReaderAsync();
    if (!await reader.ReadAsync())
        return Results.Unauthorized();                     // 그런 아이디 없음

    long   userId     = reader.GetInt64(0);
    string storedHash = reader.GetString(1);

    // 입력한 비번을 같은 방식으로 다져서, 저장된 해시와 비교
    if (!BCrypt.Net.BCrypt.Verify(req.Password, storedHash))
        return Results.Unauthorized();                     // 비번 틀림

    return Results.Ok(new { userId, username = req.Username });
});

// =====================================================================
// 5) 저장: 유니티가 SaveData JSON 을 그대로 보내면 DB에 나눠 담음
//    주소 예: POST /save/1/0  (1 = userId, 0 = 세이브 슬롯(0~9), 본문 = JsonUtility.ToJson(saveData))
//
//    하이브리드 방식 (공부노트 4번):
//    - 골드/레벨/파츠/미사일 = 검증 대상 → 테이블 칸에 따로따로
//    - 호감도/스킬 = 보관만 → extra_json 한 칸에 쪽지째
//
//    슬롯은 UPSERT — 그 슬롯이 처음이면 새로 생기고(INSERT), 이미 있으면 덮어씀(UPDATE).
// =====================================================================
app.MapPost("/save/{userId:long}/{slot:int}", async (long userId, int slot, SaveDataDto data) =>
{
    if (slot < 0 || slot > 9)
        return Results.BadRequest(new { error = "슬롯은 0~9 사이여야 합니다." });

    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    // 트랜잭션 = "전부 성공 아니면 전부 취소" 묶음.
    // 골드는 저장됐는데 파츠 저장 중 에러나면 반쪽짜리 세이브가 되므로,
    // 중간에 실패하면 몽땅 원상복구되도록 묶는다. (은행 이체와 같은 원리)
    await using var tx = await conn.BeginTransactionAsync();

    try
    {
        // (1) 호감도/스킬은 JSON 쪽지로 만들어 extra_json 칸에
        string extraJson = JsonSerializer.Serialize(new
        {
            affections = data.Affections ?? [],
            skills     = data.Skills     ?? []
        }, JsonSerializerOptions.Web);   // 항목 이름을 소문자 시작으로 통일 (skillId, npc...)

        // (2) 플레이어 스탯: 그 슬롯이 없으면 새로 만들고(INSERT), 있으면 덮어씀(UPDATE) — UPSERT
        await using var up = new MySqlCommand(@"
            INSERT INTO player_save
                (user_id, slot, level, exp, exp_to_next_level, cur_hp, cur_shield, cur_armor, cur_boost, gold, extra_json)
            VALUES
                (@id, @sl, @lv, @xp, @xn, @hp, @sh, @am, @bo, @gd, @ex)
            ON DUPLICATE KEY UPDATE
                level = @lv, exp = @xp, exp_to_next_level = @xn,
                cur_hp = @hp, cur_shield = @sh, cur_armor = @am, cur_boost = @bo,
                gold = @gd, extra_json = @ex;", conn, tx);
        up.Parameters.AddWithValue("@id", userId);
        up.Parameters.AddWithValue("@sl", slot);
        up.Parameters.AddWithValue("@lv", data.Level);
        up.Parameters.AddWithValue("@xp", data.Exp);
        up.Parameters.AddWithValue("@xn", data.ExpToNextLevel);
        up.Parameters.AddWithValue("@hp", data.CurHp);
        up.Parameters.AddWithValue("@sh", data.CurShield);
        up.Parameters.AddWithValue("@am", data.CurArmor);
        up.Parameters.AddWithValue("@bo", data.CurBoost);
        up.Parameters.AddWithValue("@gd", data.Gold);
        up.Parameters.AddWithValue("@ex", extraJson);

        try
        {
            await up.ExecuteNonQueryAsync();
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoReferencedRow2)
        {
            // user_id 자체가 users 테이블에 없음 = 가입 안 된 유저
            await tx.RollbackAsync();
            return Results.NotFound(new { error = $"userId {userId} 는 가입되지 않은 유저입니다." });
        }

        // (3) 파츠 슬롯: 이 세이브 슬롯 것만 싹 지우고 → 새로 몽땅 넣기 (덮어쓰기 패턴)
        await using (var del = new MySqlCommand(
            "DELETE FROM part_slot WHERE user_id = @id AND slot = @sl;", conn, tx))
        {
            del.Parameters.AddWithValue("@id", userId);
            del.Parameters.AddWithValue("@sl", slot);
            await del.ExecuteNonQueryAsync();
        }
        foreach (var ps in data.PartSlots ?? [])
        {
            await using var ins = new MySqlCommand(
                "INSERT INTO part_slot (user_id, slot, slot_type, part_id) VALUES (@id, @sl, @st, @pt);", conn, tx);
            ins.Parameters.AddWithValue("@id", userId);
            ins.Parameters.AddWithValue("@sl", slot);
            ins.Parameters.AddWithValue("@st", ps.SlotType);
            ins.Parameters.AddWithValue("@pt", ps.PartId);
            await ins.ExecuteNonQueryAsync();
        }

        // (4) 미사일 슬롯: 같은 덮어쓰기 패턴
        await using (var del = new MySqlCommand(
            "DELETE FROM missile_slot WHERE user_id = @id AND slot = @sl;", conn, tx))
        {
            del.Parameters.AddWithValue("@id", userId);
            del.Parameters.AddWithValue("@sl", slot);
            await del.ExecuteNonQueryAsync();
        }
        foreach (var m in data.MissileSlots ?? [])
        {
            await using var ins = new MySqlCommand(@"
                INSERT INTO missile_slot (user_id, slot, type, missile_data_id, cur_ammo, max_ammo)
                VALUES (@id, @sl, @ty, @md, @ca, @ma);", conn, tx);
            ins.Parameters.AddWithValue("@id", userId);
            ins.Parameters.AddWithValue("@sl", slot);
            ins.Parameters.AddWithValue("@ty", m.Type);
            ins.Parameters.AddWithValue("@md", m.MissileDataId);
            ins.Parameters.AddWithValue("@ca", m.CurAmmo);
            ins.Parameters.AddWithValue("@ma", m.MaxAmmo);
            await ins.ExecuteNonQueryAsync();
        }

        // (5) 여기까지 무사히 왔으면 확정 도장
        await tx.CommitAsync();
        return Results.Ok(new { saved = true, userId, slot });
    }
    catch (Exception ex)
    {
        await tx.RollbackAsync();   // 중간에 뭐든 실패하면 몽땅 취소
        return Results.Problem("저장 실패: " + ex.Message);
    }
});

// =====================================================================
// 6) 로드: DB에서 꺼내 SaveData 모양 그대로 돌려줌
//    주소 예: GET /load/1/0  (1 = userId, 0 = 세이브 슬롯)
//    유니티는 응답을 JsonUtility.FromJson<SaveData>() 에 바로 넣으면 됨
// =====================================================================
app.MapGet("/load/{userId:long}/{slot:int}", async (long userId, int slot) =>
{
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    // (1) 플레이어 스탯 + extra_json 읽기
    int level, exp, expToNext, hp, shield, armor, gold;
    float boost;
    string? extraJson;

    await using (var cmd = new MySqlCommand(@"
        SELECT level, exp, exp_to_next_level, cur_hp, cur_shield, cur_armor,
               cur_boost, gold, extra_json
        FROM player_save WHERE user_id = @id AND slot = @sl;", conn))
    {
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@sl", slot);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync())
            return Results.NotFound(new { error = $"userId {userId} 의 슬롯 {slot} 세이브가 없습니다." });

        level     = r.GetInt32(0);
        exp       = r.GetInt32(1);
        expToNext = r.GetInt32(2);
        hp        = r.GetInt32(3);
        shield    = r.GetInt32(4);
        armor     = r.GetInt32(5);
        boost     = r.GetFloat(6);
        gold      = r.GetInt32(7);
        extraJson = r.IsDBNull(8) ? null : r.GetString(8);
    }

    // (2) 파츠 슬롯 읽기
    var partSlots = new List<PartSlotDto>();
    await using (var cmd = new MySqlCommand(
        "SELECT slot_type, part_id FROM part_slot WHERE user_id = @id AND slot = @sl;", conn))
    {
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@sl", slot);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            partSlots.Add(new PartSlotDto(r.GetInt32(0), r.GetInt32(1)));
    }

    // (3) 미사일 슬롯 읽기
    var missileSlots = new List<MissileSlotDto>();
    await using (var cmd = new MySqlCommand(
        "SELECT type, missile_data_id, cur_ammo, max_ammo FROM missile_slot WHERE user_id = @id AND slot = @sl;", conn))
    {
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.Parameters.AddWithValue("@sl", slot);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            missileSlots.Add(new MissileSlotDto(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3)));
    }

    // (4) extra_json 쪽지를 풀어서 호감도/스킬 꺼내기
    AffectionDto[] affections = [];
    SkillDto[]     skills     = [];
    if (!string.IsNullOrEmpty(extraJson))
    {
        var extra = JsonSerializer.Deserialize<ExtraJson>(extraJson, JsonSerializerOptions.Web);
        affections = extra?.Affections ?? [];
        skills     = extra?.Skills     ?? [];
    }

    // (5) SaveData 와 똑같은 모양으로 조립해서 반환
    return Results.Ok(new SaveDataDto(
        level, exp, expToNext, hp, shield, armor, boost, gold,
        affections, skills, partSlots.ToArray(), missileSlots.ToArray()));
});

// =====================================================================
// 7) 세이브 목록: 로비에서 "플레이" 누르면 뜨는 로드 화면용.
//    그 유저가 가진 슬롯들의 요약(레벨/골드/저장시각)만 돌려줌 — 실제 저장 안 된
//    슬롯은 목록에 없음. 유니티가 0~9 중 빠진 번호를 "빈 슬롯(새 게임)"으로 표시.
//    주소 예: GET /saves/1  (1 = userId)
// =====================================================================
app.MapGet("/saves/{userId:long}", async (long userId) =>
{
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();

    var saves = new List<SaveSummaryDto>();
    await using var cmd = new MySqlCommand(@"
        SELECT slot, level, gold, updated_at
        FROM player_save WHERE user_id = @id ORDER BY slot;", conn);
    cmd.Parameters.AddWithValue("@id", userId);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        saves.Add(new SaveSummaryDto(
            r.GetInt32(0), r.GetInt32(1), r.GetInt32(2),
            r.GetDateTime(3).ToString("yyyy-MM-dd HH:mm:ss")));
    }

    return Results.Ok(saves);
});

// =====================================================================
// 8) 세이브 슬롯 삭제 (선택 — 로드 화면에서 "삭제" 버튼용)
// =====================================================================
app.MapDelete("/save/{userId:long}/{slot:int}", async (long userId, int slot) =>
{
    await using var conn = new MySqlConnection(connString);
    await conn.OpenAsync();
    await using var cmd = new MySqlCommand(
        "DELETE FROM player_save WHERE user_id = @id AND slot = @sl;", conn);
    cmd.Parameters.AddWithValue("@id", userId);
    cmd.Parameters.AddWithValue("@sl", slot);
    int affected = await cmd.ExecuteNonQueryAsync();   // part_slot/missile_slot도 FK CASCADE로 같이 지워짐

    if (affected == 0)
        return Results.NotFound(new { error = $"userId {userId} 의 슬롯 {slot} 세이브가 없습니다." });

    return Results.Ok(new { deleted = true, userId, slot });
});

app.Run();

// =====================================================================
// 요청/응답 JSON 을 담는 그릇들.
// ★ 필드 이름이 유니티 SaveData.cs 와 똑같아야 함 (level, curHp, partSlots...)
//   — ASP.NET이 응답을 자동으로 소문자 시작(camelCase)으로 바꿔주므로
//     Level → "level" 로 나가 유니티 JsonUtility 와 딱 맞음.
//   enum 값(PART_TYPE 등)은 유니티 JsonUtility 가 숫자로 저장하므로 int 로 받음.
// =====================================================================
record RegisterRequest(string Username, string Password);
record LoginRequest(string Username, string Password);

record SaveDataDto(
    int Level, int Exp, int ExpToNextLevel,
    int CurHp, int CurShield, int CurArmor, float CurBoost,
    int Gold,
    AffectionDto[]? Affections,
    SkillDto[]?     Skills,
    PartSlotDto[]?  PartSlots,
    MissileSlotDto[]? MissileSlots);

record AffectionDto(int Npc, int Value);
record SkillDto(int SkillId, int SlotIndex);
record PartSlotDto(int SlotType, int PartId);
record MissileSlotDto(int Type, int MissileDataId, int CurAmmo, int MaxAmmo);

// 로드 화면(슬롯 목록)용 요약 — 슬롯 번호/레벨/골드/저장시각만
record SaveSummaryDto(int Slot, int Level, int Gold, string UpdatedAt);

// extra_json 쪽지 내용물 ({"affections":[...],"skills":[...]})
record ExtraJson(AffectionDto[]? Affections, SkillDto[]? Skills);
