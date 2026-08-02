using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OutGame.Flow
{
    /// <summary>
    /// Resources.Load(All) 경로 상수 — 여러 Flow 컨트롤러가 같은 경로 문자열을 각자 하드코딩하다
    /// 어긋나는 것을 막는다(SceneNames.cs와 동일한 목적, §3.1).
    /// </summary>
    internal static class ResourcePaths
    {
        public const string RunConfigDefault = "OutGame/Data/RunConfig_Default";
        public const string EnemyCompositionConfigDefault = "OutGame/Data/EnemyCompositionConfig_Default";
        public const string PresetGradeConfigDefault = "OutGame/Data/PresetGradeConfig_Default";
        public const string EnemyPresets = "OutGame/Data/EnemyPresets";
        public const string ItemDropConfigDefault = "OutGame/Data/ItemDropConfig_Default";
        public const string RoomTypeVisuals = "OutGame/RoomTypeVisuals";
        public const string Maps = "OutGame/Data/Maps";
        public const string Events = "OutGame/Data/Events";
        public const string Augments = "OutGame/Data/Augments";
        public const string Characters = "OutGame/Data/Characters";
        public const string Data = "OutGame/Data"; // ArmyDefinition/ItemDefinition 공용 루트
    }

    /// <summary>
    /// Resources.LoadAll&lt;T&gt;() → 빈 값이면 즉시 예외를 던지는 fail-fast 패턴을 한 곳에 모은다
    /// (여러 Flow 컨트롤러에 반복되던 보일러플레이트, 코드 리뷰 MEDIUM 지적 반영).
    /// </summary>
    internal static class ResourcePool
    {
        public static List<T> LoadAllOrThrow<T>(string path, string errorMessage) where T : UnityEngine.Object
        {
            List<T> pool = Resources.LoadAll<T>(path).ToList();
            if (pool.Count == 0)
                throw new InvalidOperationException(errorMessage);
            return pool;
        }
    }
}
