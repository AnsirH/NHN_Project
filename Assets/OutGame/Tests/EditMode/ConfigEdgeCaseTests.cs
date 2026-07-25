using System;
using System.Linq;
using NUnit.Framework;
using OutGame.Logic.Maps;

namespace OutGame.Tests.EditMode
{
    /// <summary>
    /// config 경계값 검증 + 가중치 0 처리 회귀 테스트 (코드 리뷰 후속).
    /// </summary>
    public class ConfigEdgeCaseTests
    {
        [Test]
        public void Validate_GridWidthBelowTwo_Throws()
        {
            var config = new MapGenerationConfig { gridWidth = 1 };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Clone_ReturnsIndependentCopyWithSameValues()
        {
            var original = new MapGenerationConfig { floorCount = 7, gridWidth = 3, mapName = "테스트맵" };

            MapGenerationConfig clone = original.Clone();
            clone.floorCount = 99;

            Assert.AreEqual(7, original.floorCount, "clone 변형이 원본에 영향을 주면 안 됨");
            Assert.AreEqual(3, clone.gridWidth);
            Assert.AreEqual("테스트맵", clone.mapName);
        }

        [Test]
        public void Validate_PathCountBelowOne_Throws()
        {
            var config = new MapGenerationConfig { pathCount = 0 };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_PreBossNodeCountExceedsGridWidth_Throws()
        {
            var config = new MapGenerationConfig { preBossNodeCount = new IntRange(1, 6) };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Validate_NonPositiveBattleWeight_Throws()
        {
            var config = new MapGenerationConfig { battleWeight = 0f };
            Assert.Throws<ArgumentException>(() => config.Validate());
        }

        [Test]
        public void Generate_ZeroRestWeight_NoRestOnProbabilityFloors()
        {
            var config = new MapGenerationConfig { restWeight = 0f };
            int fixedRestFloor = config.floorCount - 2;

            for (int seed = 0; seed < 30; seed++)
            {
                MapState map = new MapGenerator(config, seed).Generate();
                var violations = map.nodes
                    .Where(n => n.roomType == RoomType.Rest && n.point.y != fixedRestFloor)
                    .ToList();

                Assert.IsEmpty(violations,
                    $"seed {seed}: restWeight=0인데 확률 배정 층에 휴식 방 생성됨");
            }
        }

        [Test]
        public void Generate_ZeroEventWeight_NoEventRooms()
        {
            var config = new MapGenerationConfig { eventWeight = 0f };

            for (int seed = 0; seed < 30; seed++)
            {
                MapState map = new MapGenerator(config, seed).Generate();
                Assert.IsFalse(map.nodes.Any(n => n.roomType == RoomType.Event),
                    $"seed {seed}: eventWeight=0인데 이벤트 방 생성됨");
            }
        }

        [Test]
        public void Generate_ZeroAugmentWeight_NoAugmentRooms()
        {
            var config = new MapGenerationConfig { augmentWeight = 0f };

            for (int seed = 0; seed < 30; seed++)
            {
                MapState map = new MapGenerator(config, seed).Generate();
                Assert.IsFalse(map.nodes.Any(n => n.roomType == RoomType.Augment),
                    $"seed {seed}: augmentWeight=0인데 증강 방 생성됨");
            }
        }

        [Test]
        public void Generate_DefaultConfig_ProducesAugmentRoomsAcrossManySeeds()
        {
            var config = new MapGenerationConfig();

            bool anyAugmentRoom = Enumerable.Range(0, 30)
                .Select(seed => new MapGenerator(config, seed).Generate())
                .Any(map => map.nodes.Any(n => n.roomType == RoomType.Augment));

            Assert.IsTrue(anyAugmentRoom, "기본 augmentWeight(20)로는 30개 시드 중 최소 1개는 증강 방이 나와야 함 (§4-27)");
        }
    }
}
