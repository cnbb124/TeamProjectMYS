using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// =====================================================================
// HubEnumEditor — enum 소스에 항목을 추가하는 공용 유틸.
//
// Hub에서 "이름만 입력하면 enum까지 등록"을 지원하려고 만든 것.
// enum은 코드라서 항목을 넣으면 컴파일이 끝난 뒤에야 그 값을 쓸 수 있음 —
// 그래서 호출자는 '추가'와 '그 값으로 등록'을 두 단계로 나눠야 함.
//
// 프로젝트 규칙:
//   · 모든 항목에 번호를 명시함(명시가 없으면 앞을 지웠을 때 뒤가 밀려 직렬화 값이 깨짐)
//   · 한 번 쓴 번호는 항목을 지워도 재사용하지 않음 → 항상 최대값 뒤에 붙임
// =====================================================================
public static class HubEnumEditor
{
	private const string EnumTypesPath = "Assets/2.Scripts/Enums/enum_Types.cs";

	/// <summary>C# 식별자로 쓸 수 있는 이름인지.</summary>
	public static bool IsValidIdentifier(string name)
	{
		return !string.IsNullOrWhiteSpace(name)
			&& Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$");
	}

	/// <summary>이미 그 enum에 있는 이름인지(대소문자 무시).</summary>
	public static bool Exists(System.Type enumType, string name)
	{
		string[] names = System.Enum.GetNames(enumType);
		for (int i = 0; i < names.Length; i++)
		{
			if (string.Equals(names[i], name, System.StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// enum에 항목을 추가함. 성공하면 true.
	/// step이 1보다 크면 그 단위로 올림해 번호를 띄움(사이에 끼워 넣을 여지를 둠).
	/// sentinelThreshold 이상인 값은 '끝을 나타내는 값'으로 보고 번호 계산에서 제외함.
	/// </summary>
	public static bool AddMember(System.Type enumType, string memberName, int step = 1, int sentinelThreshold = int.MaxValue)
	{
		if (!IsValidIdentifier(memberName))
		{
			Debug.LogError($"[Hub] '{memberName}'은 C# 식별자로 쓸 수 없음 — 영문/숫자/밑줄만, 숫자로 시작 불가.");
			return false;
		}
		if (Exists(enumType, memberName))
		{
			Debug.LogWarning($"[Hub] {enumType.Name}에 이미 '{memberName}'이 있음.");
			return false;
		}

		string fullPath = System.IO.Path.GetFullPath(EnumTypesPath);
		if (!System.IO.File.Exists(fullPath))
		{
			Debug.LogError($"[Hub] enum 파일을 찾지 못함: {EnumTypesPath}");
			return false;
		}

		string text = System.IO.File.ReadAllText(fullPath);

		// 대상 enum 블록의 범위를 찾음 — 선언부부터 첫 닫는 중괄호까지
		Match declaration = Regex.Match(text, $@"(?m)^\s*public\s+enum\s+{Regex.Escape(enumType.Name)}\s*(?:\r?\n)?\s*\{{");
		if (!declaration.Success)
		{
			Debug.LogError($"[Hub] enum_Types.cs에서 '{enumType.Name}' 선언을 못 찾아 중단함.");
			return false;
		}
		int bodyStart = declaration.Index + declaration.Length;
		int bodyEnd = text.IndexOf('}', bodyStart);
		if (bodyEnd < 0)
		{
			Debug.LogError($"[Hub] '{enumType.Name}' 블록의 끝을 못 찾아 중단함.");
			return false;
		}

		int next = NextFreeValue(enumType, step, sentinelThreshold);

		// 마지막 항목 뒤에 붙임. 들여쓰기는 블록 안의 기존 항목에서 따옴
		string body = text.Substring(bodyStart, bodyEnd - bodyStart);
		Match indentMatch = Regex.Match(body, @"(?m)^([ \t]+)\S");
		string indent = indentMatch.Success ? indentMatch.Groups[1].Value : "\t";

		// 센티넬(끝값)이 있으면 그 앞에, 없으면 블록 끝에 삽입
		int insertAt = bodyEnd;
		if (sentinelThreshold != int.MaxValue)
		{
			Match sentinel = FindSentinelLine(text, bodyStart, bodyEnd, enumType, sentinelThreshold);
			if (sentinel.Success)
			{
				insertAt = sentinel.Index;
			}
		}

		string insert = $"{indent}{memberName} = {next},{System.Environment.NewLine}";
		// 블록 끝에 붙일 때는 마지막 항목 줄바꿈 뒤로 들어가도록 앞에 개행을 넣지 않음
		text = text.Insert(insertAt, insert);

		System.IO.File.WriteAllText(fullPath, text, System.Text.Encoding.UTF8);
		AssetDatabase.ImportAsset(EnumTypesPath);
		Debug.Log($"[Hub] {enumType.Name}에 추가함: {memberName} = {next}  (컴파일 후 사용 가능)");
		return true;
	}

	// 센티넬 임계값 이상인 항목의 줄 시작 위치를 찾음(그 앞에 새 항목을 끼우기 위함).
	private static Match FindSentinelLine(string text, int bodyStart, int bodyEnd, System.Type enumType, int threshold)
	{
		foreach (object v in System.Enum.GetValues(enumType))
		{
			if ((int)v < threshold)
			{
				continue;
			}
			string name = System.Enum.GetName(enumType, v);
			if (string.IsNullOrEmpty(name))
			{
				continue;
			}
			Match m = Regex.Match(text.Substring(bodyStart, bodyEnd - bodyStart),
				$@"(?m)^[ \t]*{Regex.Escape(name)}\s*=");
			if (m.Success)
			{
				// 원문 기준 인덱스로 환산
				return Regex.Match(text, $@"(?m)^[ \t]*{Regex.Escape(name)}\s*=");
			}
		}
		return Match.Empty;
	}

	/// <summary>다음에 쓸 번호. 최대값을 step 단위로 올린 값.</summary>
	public static int NextFreeValue(System.Type enumType, int step = 1, int sentinelThreshold = int.MaxValue)
	{
		int maxUsed = -1;
		foreach (object v in System.Enum.GetValues(enumType))
		{
			int n = (int)v;
			if (n >= sentinelThreshold)
			{
				continue;
			}
			if (n > maxUsed)
			{
				maxUsed = n;
			}
		}
		if (step <= 1)
		{
			return maxUsed + 1;
		}
		return ((maxUsed / step) + 1) * step;
	}

	/// <summary>컴파일 대기 중인지. true면 enum 추가 직후라 새 값을 아직 못 씀.</summary>
	public static bool IsCompiling
	{
		get
		{
			return EditorApplication.isCompiling || EditorApplication.isUpdating;
		}
	}
}
