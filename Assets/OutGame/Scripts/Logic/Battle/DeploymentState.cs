using System;
using System.Collections.Generic;
using System.Linq;
using OutGame.Logic.Armies;
using OutGame.Logic.Augments;
using OutGame.Logic.Characters;
using OutGame.Logic.Items;
using OutGame.Logic.Maps;
using OutGame.Logic.Runs;

namespace OutGame.Logic.Battle
{
    /// <summary>
    /// 배치 편집 모델 (§5.7) — 배치 UI의 드래그 앤 드롭 결과를 담는 순수 로직.
    /// 규칙: 목록→빈 슬롯 배치, 슬롯↔슬롯 이동/스왑, 목록→점유 슬롯은 기존 부대를 목록으로 밀어냄,
    /// 최소 1개 부대 배치해야 전투 시작 가능.
    /// </summary>
    public class DeploymentState
    {
        private readonly HashSet<int> validSlotIds;
        private readonly Dictionary<int, (float x, float y)> slotPositions;
        private readonly Dictionary<int, string> slotToArmy = new Dictionary<int, string>();
        private readonly Dictionary<string, int> armyToSlot = new Dictionary<string, int>();

        public DeploymentState(IReadOnlyList<SlotDefinition> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            validSlotIds = new HashSet<int>(slots.Select(s => s.slotId));
            slotPositions = slots.ToDictionary(s => s.slotId, s => (s.x, s.y));
        }

        public int DeployedCount => slotToArmy.Count;
        public bool CanStartBattle => DeployedCount > 0;
        public int SlotCount => validSlotIds.Count;

        /// <summary>현재 배치 전체(armyInstanceId → slotId) — 영속화(§5.7)용 읽기 전용 뷰.</summary>
        public IReadOnlyDictionary<string, int> Placements => armyToSlot;

        /// <summary>이 배치판(현재 BattleFieldConfig 기준)에 존재하는 슬롯인지 — 저장된 배치 복원 시
        /// slotId가 그리드 크기 변경 전 저장분일 수 있어 방어적으로 검증해야 한다 (§5.7).</summary>
        public bool IsValidSlot(int slotId) => validSlotIds.Contains(slotId);

        public string GetArmyAt(int slotId) =>
            slotToArmy.TryGetValue(slotId, out string armyId) ? armyId : null;

        public int? GetSlotOf(string armyInstanceId) =>
            armyToSlot.TryGetValue(armyInstanceId, out int slotId) ? slotId : (int?)null;

        /// <summary>부대를 슬롯에 놓는다 — 이동/스왑/밀어내기 규칙 포함.</summary>
        public void Place(string armyInstanceId, int slotId)
        {
            if (string.IsNullOrEmpty(armyInstanceId))
                throw new ArgumentException("armyInstanceId가 비어 있습니다.", nameof(armyInstanceId));
            if (!validSlotIds.Contains(slotId))
                throw new ArgumentException($"존재하지 않는 슬롯입니다: {slotId}", nameof(slotId));

            int? currentSlot = GetSlotOf(armyInstanceId);
            if (currentSlot.HasValue && currentSlot.Value == slotId)
                return; // 이미 그 자리 — 아무 것도 하지 않음

            string occupant = GetArmyAt(slotId);

            if (currentSlot.HasValue)
            {
                // 슬롯 간 이동/스왑
                slotToArmy.Remove(currentSlot.Value);
                if (occupant != null)
                {
                    slotToArmy[currentSlot.Value] = occupant;
                    armyToSlot[occupant] = currentSlot.Value;
                }
            }
            else if (occupant != null)
            {
                // 목록 → 점유 슬롯: 기존 점유자를 목록으로 밀어냄
                armyToSlot.Remove(occupant);
            }

            slotToArmy[slotId] = armyInstanceId;
            armyToSlot[armyInstanceId] = slotId;
        }

        /// <summary>배치 해제 (슬롯 → 목록).</summary>
        public void Remove(string armyInstanceId)
        {
            int? slot = GetSlotOf(armyInstanceId);
            if (!slot.HasValue) return;

            slotToArmy.Remove(slot.Value);
            armyToSlot.Remove(armyInstanceId);
        }

        /// <summary>
        /// 현재 배치를 DeployedArmy 목록으로 변환한다 — 전투 시작 가능 여부/방 타입 제약 없이 항상
        /// 호출 가능하다(배치 화면의 실시간 전투력 미리보기 등에 사용, §4-28). BuildSetup은 이 결과에
        /// §7 전송 조건(방 타입, 최소 1개 배치)을 추가로 검사해서 쓴다 — 변환 로직 자체는 여기 하나뿐.
        /// </summary>
        public List<DeployedArmy> BuildDeployedArmies(
            RunState run, IReadOnlyDictionary<string, ItemData> items, IReadOnlyDictionary<string, ArmyData> armyDefs,
            IReadOnlyList<AugmentData> selectedAugments)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (armyDefs == null) throw new ArgumentNullException(nameof(armyDefs));
            if (selectedAugments == null) throw new ArgumentNullException(nameof(selectedAugments));

            var result = new List<DeployedArmy>();

            // slotId 오름차순 고정 — Dictionary 열거 순서는 계약이 아니므로 인게임에 넘길 리스트
            // 순서를 여기서 결정적으로 만든다 (§7.1: 직렬화된 계약의 형태가 안정적이어야 함).
            foreach (int slotId in slotToArmy.Keys.OrderBy(id => id))
            {
                string armyInstanceId = slotToArmy[slotId];
                ArmyInstance army = run.GetArmy(armyInstanceId);
                if (army == null)
                    throw new ArgumentException($"RunState에 없는 부대가 배치돼 있습니다: {armyInstanceId}");
                if (!armyDefs.TryGetValue(army.armyDefId, out ArmyData def))
                    throw new ArgumentException($"정의되지 않은 ArmyDefinition: {army.armyDefId}");

                ItemData item = ItemEquipService.ResolveItem(army, items);
                ArmyClass armyClass = item?.armyClass ?? ArmyClass.None;
                (float x, float y) = slotPositions[slotId];

                // 장군·유닛 최종 스탯(§7.1) — ArmyInfoPopup/BattlePowerCalculator와 동일한
                // ArmyStatCalculator를 공유해 계산한다(2026-07-26 확정: 인게임이 재계산하지 않도록
                // 아웃게임이 최종값을 그대로 넘긴다). 장군도 증강 혜택을 받으므로 유닛과 같은 배율.
                float healthMultiplier = ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, armyClass, AugmentStat.Health, selectedAugments);
                float attackMultiplier = ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, armyClass, AugmentStat.Attack, selectedAugments);
                float defenseMultiplier = ArmyStatCalculator.GetStatMultiplier(army.upgradeLevel, armyClass, AugmentStat.Defense, selectedAugments);

                result.Add(new DeployedArmy
                {
                    armyInstanceId = armyInstanceId,
                    armyDefId = army.armyDefId,
                    armyClass = armyClass,
                    equippedItemId = army.EquippedItemId,
                    generalSkillId = item?.generalSkillId,
                    soldierCount = def.baseSoldierCount + army.bonusSoldierCount,
                    upgradeLevel = army.upgradeLevel,
                    generalSkillUpgradeCount = AugmentTargeting.CountMatching(
                        armyClass, AugmentEffectType.GeneralSkillUpgrade, selectedAugments),
                    generalHealth = def.generalHealth * healthMultiplier,
                    generalAttack = def.generalAttack * attackMultiplier,
                    generalDefense = def.generalDefense * defenseMultiplier,
                    generalCritRate = def.generalCritRate,
                    generalMoveSpeed = def.generalMoveSpeed,
                    soldierHealth = def.soldierHealth * healthMultiplier,
                    soldierAttack = def.soldierAttack * attackMultiplier,
                    soldierDefense = def.soldierDefense * defenseMultiplier,
                    slotId = slotId,
                    slotX = x,
                    slotY = y,
                });
            }

            return result;
        }

        /// <summary>배치 확정 → 인게임 전달 데이터 생성 (§7.1). 전투/보스 방이 아니거나 배치가 없으면 예외.</summary>
        public BattleSetupData BuildSetup(
            string roomId,
            RoomType roomType,
            string encounterId,
            RunState run,
            IReadOnlyDictionary<string, ItemData> items,
            IReadOnlyDictionary<string, ArmyData> armyDefs,
            IReadOnlyList<AugmentData> selectedAugments,
            IReadOnlyDictionary<string, PlayerCharacterData> characterDataById)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (characterDataById == null) throw new ArgumentNullException(nameof(characterDataById));
            if (roomType != RoomType.NormalBattle && roomType != RoomType.Boss)
                throw new ArgumentException(
                    $"배치는 전투/보스 방에서만 가능합니다 (§4-8). 요청 타입: {roomType}", nameof(roomType));
            if (!CanStartBattle)
                throw new InvalidOperationException("최소 1개 부대를 배치해야 전투를 시작할 수 있습니다 (§5.7).");
            if (string.IsNullOrWhiteSpace(run.selectedCharacterId))
                throw new ArgumentException(
                    "RunState.selectedCharacterId가 비어 있습니다 — 캐릭터 선택 화면(§5.2.5)을 거치지 않았습니다.", nameof(run));
            if (!characterDataById.TryGetValue(run.selectedCharacterId, out PlayerCharacterData character))
                throw new ArgumentException(
                    $"선택된 캐릭터 '{run.selectedCharacterId}'를 찾을 수 없습니다 (§5.2.5).", nameof(run));

            var setup = new BattleSetupData
            {
                roomId = roomId,
                roomType = roomType,
                encounterId = encounterId,
                playerCharacterId = character.id,
                playerCharacterSkillId = character.skillId,
            };
            setup.armies.AddRange(BuildDeployedArmies(run, items, armyDefs, selectedAugments));

            return setup;
        }
    }
}
