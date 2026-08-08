using System;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using CrossAccel.Core;
using CrossAccel.Data;
using NUnit.Framework;

namespace CrossAccel.Tests
{
    /// <summary>
    /// Phase 3 — 액션 페이즈 발동 순서와 챌린지 해결 검증 (TESTING.md 3단계).
    /// 근거: docs/RULES.md 5-3, 6번, 8번, R1, R5~R8, R10.
    /// </summary>
    public class ChallengeTests
    {
        private GameManager _game;
        private readonly List<string> _activated = new List<string>();

        [SetUp]
        public void SetUp()
        {
            _activated.Clear();
            _game = new GameManager(new CardDatabase(), new Random(1));

            // 효과 구현은 Phase 4. 여기서는 "무엇이 어떤 순서로 발동됐는가"만 기록하는 스파이를 꽂는다.
            _game.SkillEffectResolver = ctx => _activated.Add($"P{ctx.Owner.PlayerId}:{ctx.Skill.Id}");
            _game.BeginReadyPhase();
        }

        private CharacterUnit AddCharacter(int playerId, string weapon, int position = 0)
        {
            var unit = new CharacterUnit(TestCards.Character($"C{playerId}_{position}", weapon), playerId, position);
            _game.Players[playerId].CharacterZone.Add(unit);
            return unit;
        }

        private ActiveSlot Submit(int playerId, SkillData skill, CharacterUnit user, bool asSwift = false)
        {
            _game.Players[playerId].Hand.Add(skill);
            Assert.IsTrue(_game.SubmitActiveCard(playerId, skill, user, asAccel: false, asSwift: asSwift),
                $"{skill.Id} 제출 실패");
            return _game.Players[playerId].ActiveZone.Last();
        }

        // ---------------- 챌린지 진입 조건 ----------------

        [Test]
        public void ActionPhase_SameSpeedFromBothPlayers_EntersChallenge()
        {
            // RULES.md 6-5: 속도가 같을 경우 챌린지 상태
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "한손검"));
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "책"));

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(1, _game.ChallengeLog.Count, "속도 동률이면 챌린지에 들어가야 함");
        }

        [Test]
        public void ActionPhase_DifferentSpeeds_NoChallengeAndResolvesFastestFirst()
        {
            // RULES.md R1: 속도가 높을수록 먼저 발동
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");

            Submit(0, TestCards.Skill("SLOW", speed: 3), u0);
            Submit(0, TestCards.Skill("FAST", speed: 7), u0);
            Submit(1, TestCards.Skill("MID", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(0, _game.ChallengeLog.Count, "속도가 다르면 챌린지 없음");
            CollectionAssert.AreEqual(new[] { "P0:FAST", "P1:MID", "P0:SLOW" }, _activated);
        }

        [Test]
        public void ActionPhase_SwiftCards_ResolveBeforeNormalCardsEvenIfSlower()
        {
            // RULES.md 7번 신속: "신속 카드는 액션 페이즈에 보통 카드보다 먼저 적용".
            // 속도가 아무리 낮아도 신속 그룹이 통째로 먼저 발동한다.
            // (예전에는 이 그룹을 엑셀로 나눴었다 — 이름만 엑셀이고 하는 일은 신속이었다. GameManager.ResolveGroup 주석 참고)
            var u0 = AddCharacter(0, "한손검");
            AddCharacter(1, "활");

            Submit(0, TestCards.Skill("NORMAL_FAST", speed: 9), u0);
            Submit(0, TestCards.Skill("SWIFT_SLOW", speed: 1), u0, asSwift: true);
            _game.RunActionPhase();

            CollectionAssert.AreEqual(new[] { "P0:SWIFT_SLOW", "P0:NORMAL_FAST" }, _activated);
        }

        // ---------------- 챌린지 승패 ----------------

        [Test]
        public void Challenge_OnlyOneSideMatchesWeapon_OnlyThatSideActivates()
        {
            // RULES.md 8-2/8-4: 발동 캐릭터와 무기 타입이 같으면 성공, 단독 성공자만 효과 발동
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "한손검")); // 성공
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "책"));     // 실패

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var slot1 = Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(0, _game.ChallengeLog[0].Winner);
            Assert.IsTrue(slot0.Activated, "단독 성공자는 발동");
            Assert.IsFalse(slot1.Activated, "실패자는 무효");
            CollectionAssert.AreEqual(new[] { "P0:A" }, _activated);
        }

        [Test]
        public void Challenge_BothSidesFailWeaponMatch_BothInvalidated()
        {
            // RULES.md R6: 양쪽 다 무기 매칭 실패 시 양쪽 무효
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "책"));
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "악기"));

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var slot1 = Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(GameManager.NoWinner, _game.ChallengeLog[0].Winner);
            Assert.IsFalse(slot0.Activated);
            Assert.IsFalse(slot1.Activated);
            CollectionAssert.IsEmpty(_activated);
        }

        [Test]
        public void Challenge_CommonWeaponCardRevealed_CountsAsFailureEvenIfNameOverlaps()
        {
            // RULES.md R7: 공용 카드는 무효(매칭 실패로 처리) — 무기 이름이 겹쳐도 성공이 아니다
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "공용, 한손검")); // 공용이므로 실패
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "활"));           // 성공

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var slot1 = Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(1, _game.ChallengeLog[0].Winner, "공용을 뒤집은 P0은 실패해야 함");
            Assert.IsFalse(slot0.Activated);
            Assert.IsTrue(slot1.Activated);
        }

        [Test]
        public void Challenge_BothSucceed_RevealsAgainUntilSomeoneFails()
        {
            // RULES.md 8-3: 서로 성공했으면 다시 덱 탑 오픈 — 실패자가 나올 때까지 반복
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.AddRange(new[]
            {
                TestCards.Skill("TOP0_1", "한손검"), // 1회차 성공
                TestCards.Skill("TOP0_2", "한손검")  // 2회차 성공
            });
            _game.Players[1].Deck.AddRange(new[]
            {
                TestCards.Skill("TOP1_1", "활"),  // 1회차 성공
                TestCards.Skill("TOP1_2", "책")   // 2회차 실패
            });

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(0, _game.ChallengeLog[0].Winner);
            Assert.IsTrue(slot0.Activated);
            Assert.AreEqual(0, _game.Players[0].Deck.Count, "2회 공개로 덱 2장 소모");
            Assert.AreEqual(0, _game.Players[1].Deck.Count);
        }

        // ---------------- 라운드 기록 (연출용 순수 가산 — 판정에 영향 없음) ----------------
        // [왜 이 기록이 필요한가] 연출(모듈 D)이 "둘 다 일치 → 재대결 N라운드"를 화면에 재현하려면
        //   라운드마다 무엇이 공개됐는지 알아야 한다. 예전엔 UI가 트래쉬 증가분으로 역산했는데
        //   스킬 부수효과가 트래쉬에 카드를 더 버리면 어긋나 깨졌다 (GameManager.ResolveChallenge 참고).

        [Test]
        public void ChallengeRounds_SingleRound_RecordsOneRoundWithRevealedWeapons()
        {
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "한손검")); // 성공
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "책"));     // 실패

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var rounds = _game.ChallengeLog[0].Rounds;
            Assert.AreEqual(1, rounds.Count, "재대결이 없으면 라운드는 1건");
            Assert.AreEqual("한손검", rounds[0].WeaponA);
            Assert.AreEqual("책", rounds[0].WeaponB);
            Assert.IsTrue(rounds[0].SuccessA);
            Assert.IsFalse(rounds[0].SuccessB);
        }

        [Test]
        public void ChallengeRounds_BothSucceed_RecordsEveryRematchRoundInOrder()
        {
            // RULES.md 8-3: 서로 성공하면 재공개 — 그 라운드들이 순서대로 다 남아야 재대결 연출이 된다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.AddRange(new[]
            {
                TestCards.Skill("TOP0_1", "한손검"), // 1라운드 성공
                TestCards.Skill("TOP0_2", "한손검")  // 2라운드 성공
            });
            _game.Players[1].Deck.AddRange(new[]
            {
                TestCards.Skill("TOP1_1", "활"),  // 1라운드 성공 → 재대결
                TestCards.Skill("TOP1_2", "책")   // 2라운드 실패 → 종료
            });

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var rounds = _game.ChallengeLog[0].Rounds;
            Assert.AreEqual(2, rounds.Count, "재대결이 1번 있었으니 라운드는 2건");

            // 1라운드: 양쪽 다 성공 (그래서 재대결로 이어졌다)
            Assert.AreEqual("한손검", rounds[0].WeaponA);
            Assert.AreEqual("활", rounds[0].WeaponB);
            Assert.IsTrue(rounds[0].SuccessA, "1라운드 양쪽 성공이라 재대결이 일어났다");
            Assert.IsTrue(rounds[0].SuccessB);

            // 2라운드: A만 성공 → 승부 결정
            Assert.AreEqual("한손검", rounds[1].WeaponA);
            Assert.AreEqual("책", rounds[1].WeaponB);
            Assert.IsTrue(rounds[1].SuccessA);
            Assert.IsFalse(rounds[1].SuccessB);
        }

        [Test]
        public void ChallengeRounds_LastRoundSuccessFlags_AlwaysAgreeWithWinner()
        {
            // 자기모순 방지: 마지막 라운드에서 성공한 쪽이 곧 Winner여야 한다 (양측 무효면 둘 다 실패).
            // 연출이 "A 성공 표시"를 띄운 뒤 "B 선공!"이라고 말하는 사고를 막는 불변식이다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.AddRange(new[]
            {
                TestCards.Skill("TOP0_1", "한손검"), // 재대결 유발
                TestCards.Skill("TOP0_2", "책")      // 2라운드 실패
            });
            _game.Players[1].Deck.AddRange(new[]
            {
                TestCards.Skill("TOP1_1", "활"),
                TestCards.Skill("TOP1_2", "활")      // 2라운드 성공 → P1 승
            });

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var record = _game.ChallengeLog[0];
            var last = record.Rounds[record.Rounds.Count - 1];

            Assert.AreEqual(1, record.Winner, "2라운드에서 P1만 성공");
            Assert.IsFalse(last.SuccessA);
            Assert.IsTrue(last.SuccessB);
            Assert.AreEqual(record.Winner == record.Slot0.OwnerId, last.SuccessA, "마지막 라운드 성공 == Winner");
            Assert.AreEqual(record.Winner == record.Slot1.OwnerId, last.SuccessB);
        }

        [Test]
        public void ChallengeRounds_BothFail_RecordsFailureForBothAndNoWinner()
        {
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "책"));
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "악기"));

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var record = _game.ChallengeLog[0];
            Assert.AreEqual(GameManager.NoWinner, record.Winner);
            Assert.AreEqual(1, record.Rounds.Count);
            Assert.IsFalse(record.Rounds[0].SuccessA, "양측 무효면 마지막 라운드에 성공한 쪽이 없다");
            Assert.IsFalse(record.Rounds[0].SuccessB);
        }

        [Test]
        public void ChallengeRounds_DeckExhausted_RecordsNullWeaponAndOpponentAutoSuccess()
        {
            // RULES.md 8-5: 덱이 마른 쪽은 공개조차 못 하고(무기 null) 상대가 자동 성공.
            // [주의] 자동 성공은 무기 매칭과 무관하다 — 아래 P1 덱탑은 사용 캐릭터(활)와 일치하지만,
            //   일치했기 때문이 아니라 P0이 공개를 못 해서 이긴 것이다. 연출은 상대 무기가 null인지
            //   함께 보고 "덱 소진"으로 표기해야 한다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "활")); // P0 덱은 0장

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var record = _game.ChallengeLog[0];
            Assert.AreEqual(1, record.Winner);
            Assert.AreEqual(1, record.Rounds.Count);
            Assert.IsNull(record.Rounds[0].RevealedA, "덱이 말라 공개 못 함");
            Assert.IsNull(record.Rounds[0].WeaponA);
            Assert.IsFalse(record.Rounds[0].SuccessA);
            Assert.AreEqual("활", record.Rounds[0].WeaponB);
            Assert.IsTrue(record.Rounds[0].SuccessB, "8-5: 상대 자동 성공");
        }

        [Test]
        public void ChallengeRounds_BothDecksExhausted_RecordsBothNullAndNoWinner()
        {
            // RULES.md 8-6
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var record = _game.ChallengeLog[0];
            Assert.AreEqual(GameManager.NoWinner, record.Winner);
            Assert.AreEqual(1, record.Rounds.Count);
            Assert.IsNull(record.Rounds[0].RevealedA);
            Assert.IsNull(record.Rounds[0].RevealedB);
            Assert.IsFalse(record.Rounds[0].SuccessA);
            Assert.IsFalse(record.Rounds[0].SuccessB);
        }

        [Test]
        public void ChallengeRounds_RecordedCardsAreTheOnesThatWentToTrash()
        {
            // 기록이 "실제로 공개된 그 카드"인지 확인 (R5: 공개 카드는 트래쉬로).
            // 라운드 기록이 트래쉬와 어긋나면 연출이 엉뚱한 무기를 보여주게 된다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            var top0 = TestCards.Skill("TOP0", "한손검");
            var top1 = TestCards.Skill("TOP1", "책");
            _game.Players[0].Deck.Add(top0);
            _game.Players[1].Deck.Add(top1);

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            var round = _game.ChallengeLog[0].Rounds[0];
            Assert.AreSame(top0, round.RevealedA, "기록된 카드가 실제 공개 카드와 같은 인스턴스");
            Assert.AreSame(top1, round.RevealedB);
            CollectionAssert.Contains(_game.Players[0].Trash, round.RevealedA);
            CollectionAssert.Contains(_game.Players[1].Trash, round.RevealedB);
        }

        [Test]
        public void ChallengeRounds_JudgmentUnchanged_RoundLogIsPureObservation()
        {
            // 기록 추가가 판정을 바꾸지 않는지: roundLog 없이 ResolveChallenge를 직접 부른 결과와
            // RunActionPhase(기록을 넘기는 경로)의 결과가 같은 규칙을 따르는지 확인한다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "한손검"));
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "책"));

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var slot1 = Submit(1, TestCards.Skill("B", speed: 5), u1);

            // roundLog를 생략한 오버로드 호출 — 컴파일되고, 판정도 정상이어야 한다.
            int winner = _game.ResolveChallenge(slot0, slot1);
            Assert.AreEqual(0, winner, "roundLog 없이도 판정은 동일 규칙");
        }

        // ---------------- R5: 공개 카드는 트래쉬로 ----------------

        [Test]
        public void Challenge_RevealedCards_GoToTrash()
        {
            // RULES.md R5
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            var top0 = TestCards.Skill("TOP0", "한손검");
            var top1 = TestCards.Skill("TOP1", "책");
            _game.Players[0].Deck.Add(top0);
            _game.Players[1].Deck.Add(top1);

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            CollectionAssert.Contains(_game.Players[0].Trash, top0);
            CollectionAssert.Contains(_game.Players[1].Trash, top1);
        }

        // ---------------- R8: 챌린지 중 덱 소진 ----------------

        [Test]
        public void Challenge_DeckExhausted_OpponentAutoWinsAndTrashIsNotRecycled()
        {
            // RULES.md 8-5 + R8: 덱이 전부 없어진 쪽은 효과 무효 + 상대 자동 성공.
            // 챌린지 중에는 트래쉬 재순환이 없으므로 트래쉬에 카드가 남아 있어도 덱은 채워지지 않는다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");

            var buried = new[] { TestCards.Skill("T1", "한손검"), TestCards.Skill("T2", "한손검") };
            _game.Players[0].Trash.AddRange(buried); // 덱은 0장, 트래쉬에만 카드가 있음
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "활"));

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var slot1 = Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(1, _game.ChallengeLog[0].Winner, "덱이 마른 P0의 상대가 자동 성공");
            Assert.IsFalse(slot0.Activated);
            Assert.IsTrue(slot1.Activated);

            Assert.AreEqual(0, _game.Players[0].Deck.Count, "R8: 챌린지 중에는 트래쉬 → 덱 재순환 없음");
            foreach (var card in buried)
                CollectionAssert.Contains(_game.Players[0].Trash, card);
        }

        [Test]
        public void Challenge_DeckExhausted_IsChallengeLossNotGameLoss()
        {
            // RULES.md 8-5는 "효과 무효 + 상대 자동 성공"까지만 정한다 — 게임 패배가 아니다.
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "활"));

            Submit(0, TestCards.Skill("A", speed: 5), u0);
            Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(GamePhase.End, _game.Phase, "덱 소진만으로 게임이 끝나면 안 됨");
            Assert.IsNull(_game.Winner);
        }

        [Test]
        public void Challenge_BothDecksExhausted_BothInvalidated()
        {
            // RULES.md 8-6: 둘 다 동시에 덱이 없어지면 둘 다 무효
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");

            var slot0 = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var slot1 = Submit(1, TestCards.Skill("B", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(GameManager.NoWinner, _game.ChallengeLog[0].Winner);
            Assert.IsFalse(slot0.Activated);
            Assert.IsFalse(slot1.Activated);
        }

        // ---------------- R10: 동속도 다중 카드 ----------------

        [Test]
        public void ActionPhase_ExtraSameSpeedCard_PairsOneToOneAndActivatesLeftover()
        {
            // RULES.md R10: 낸 순서대로 1:1 짝지어 챌린지, 짝 없는 카드는 챌린지 없이 발동
            var u0 = AddCharacter(0, "한손검");
            var u1 = AddCharacter(1, "활");
            _game.Players[0].Deck.Add(TestCards.Skill("TOP0", "한손검")); // P0 챌린지 성공
            _game.Players[1].Deck.Add(TestCards.Skill("TOP1", "책"));     // P1 챌린지 실패

            var first = Submit(0, TestCards.Skill("A", speed: 5), u0);
            var leftover = Submit(0, TestCards.Skill("B", speed: 5), u0);
            var opponentCard = Submit(1, TestCards.Skill("X", speed: 5), u1);
            _game.RunActionPhase();

            Assert.AreEqual(1, _game.ChallengeLog.Count, "챌린지는 1:1 한 건만");
            Assert.AreSame(first, _game.ChallengeLog[0].Slot0, "낸 순서상 첫 카드가 짝이 됨");
            Assert.IsTrue(first.Activated, "챌린지 승자");
            Assert.IsTrue(leftover.Activated, "짝이 없는 카드는 챌린지 없이 발동");
            Assert.IsFalse(opponentCard.Activated, "챌린지 패자");
        }

        // ---------------- 코스트 불발 ----------------

        [Test]
        public void ActionPhase_NotEnoughCost_CardMisfiresAndGoesToTrash()
        {
            // RULES.md 7번 코스트: 지불할 코스트가 없으면 그 카드 효과는 불발
            var u0 = AddCharacter(0, "한손검");
            AddCharacter(1, "활");

            var expensive = TestCards.Skill("EXPENSIVE", speed: 5, cost: 3);
            var slot = Submit(0, expensive, u0); // 코스트존이 비어 있음
            _game.RunActionPhase();

            Assert.IsFalse(slot.Activated);
            CollectionAssert.IsEmpty(_activated);
            CollectionAssert.Contains(_game.Players[0].Trash, expensive);
        }
    }
}
