using System;
using System.IO;
using UnityEngine;

namespace OutGame.Logic.Runs
{
    /// <summary>
    /// RunState 파일 저장/이어하기 (§6, §5.1). 손상된 저장 파일은 "저장 없음"과 동일하게 취급한다.
    /// </summary>
    public static class RunSaveService
    {
        private const string SaveFileName = "run_save.json";

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, SaveFileName);

        /// <summary>
        /// RunState를 JSON으로 저장한다. 임시 파일에 쓴 뒤 교체하는 방식으로,
        /// 저장 도중 중단되어도 기존 저장 파일이 반쪽짜리로 남지 않게 한다.
        /// 디스크 공간 부족·권한 등으로 인한 IOException/UnauthorizedAccessException은 호출자에게 전파된다.
        /// </summary>
        public static void Save(RunState run, string path)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("path가 비어 있습니다.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, run.ToJson());

            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }

        public static bool HasSave(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);

        /// <summary>저장 파일이 없거나 손상되었거나 읽기에 실패하면 null을 반환한다 (이어하기 불가로 처리).</summary>
        public static RunState Load(string path)
        {
            if (!HasSave(path)) return null;

            try
            {
                return RunState.FromJson(File.ReadAllText(path));
            }
            catch (ArgumentException)
            {
                return null; // 손상된 저장 파일 — 이어하기 불가로 처리
            }
            catch (IOException)
            {
                return null; // 파일이 삭제/잠김 등으로 읽기 실패 — 이어하기 불가로 처리
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static void DeleteSave(string path)
        {
            if (HasSave(path)) File.Delete(path);
        }
    }
}
