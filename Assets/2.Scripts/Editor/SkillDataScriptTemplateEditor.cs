
using System.IO;
using UnityEditor;


public class SkillDataScriptTemplateEditor : AssetModificationProcessor
{
	// 새 에셋이 생성될 때 호출되는 콜백 함수
	public static void OnWillCreateAsset(string assetPath)
	{
		// 생성되는 파일이 C# 스크립트인지 확인
		if (!assetPath.EndsWith(".cs.meta"))
		{
			return;
		}

		// .meta 확장자를 제외한 실제 스크립트 파일 경로
		string actualScriptPath = assetPath.Substring(0, assetPath.LastIndexOf(".meta"));

		// 파일 이름을 확장자 없이 추출 LaserSkillData등
		string className = Path.GetFileNameWithoutExtension(actualScriptPath);

		// 파일 이름이 "Data"로 끝나는 경우에만 처리
		if (className.EndsWith("Data") && className.Length > 4)
		{
			// 뒤의 "Data"를 제외한 앞부분만 추출 LaserSkill등
			string skillName = className.Substring(0, className.Length - 4);

			// 스크립트 파일의 전체 내용을 읽기
			string fileContent = File.ReadAllText(actualScriptPath);

			// 설정해둔 #SKILLNAME# 키워드를 추출한 문자열로 치환
			if (fileContent.Contains("#SKILLNAME#"))
			{
				fileContent = fileContent.Replace("#SKILLNAME#", skillName);

				// 변경된 내용을 파일에 다시 저장
				File.WriteAllText(actualScriptPath, fileContent);

				// 데이터 변경
				AssetDatabase.Refresh();
			}
		}
	}
}
