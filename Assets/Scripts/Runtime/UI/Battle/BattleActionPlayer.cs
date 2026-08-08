// 담당 모듈: D (전투 비주얼/연출) — UNITY_PORTING_SPEC.md 5절 모듈 D / docs/full_prototype.html
// 의존: GameManager(모듈 C가 뚫어준 훅 3개), CharacterCardView·ActSlotView(모듈 B/C의 뷰)
// 경계: 판정·수치는 전부 엔진(GameManager) 값을 그대로 읽는다. 여기는 "언제·얼마나 느리게 보여줄지"만 정한다.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CrossAccel.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace CrossAccel.UI
{
    /// <summary>
    /// [무엇] 전투 연출 재생기 — 모듈 D 전체의 뼈대.
    /// [왜] GameManager.OnSlotActivating/OnChallengeResolved/OnCharacterDied 세 훅은
    ///      <see cref="GameManager.RunActionPhase"/> 실행 도중 전부 동기(synchronous) 호출된다.
    ///      엔진은 액션+엔드 페이즈를 한 번의 호출로 끝내고 반환하므로, 훅 핸들러 안에서
    ///      "코루틴으로 잠깐 기다렸다가 다음 걸 처리"하는 건 불가능하다 — 핸들러가 반환하는 순간
    ///      엔진은 이미 다음 슬롯을 처리하러 가버리고, 그 사이 캐릭터 HP 등 상태는 전부 바뀐 뒤다.
    ///      그래서 구조를 뒤집었다: <b>훅에서는 "무엇이 일어났는지"만 큐에 적어두고(Enqueue*),
    ///      RunActionPhase가 완전히 반환한 뒤에야 그 큐를 코루틴(<see cref="Play"/>)으로 하나씩
    ///      꺼내 재생한다.</b> 재생 시점엔 이미 엔진 상태가 최종값이므로, 화면을 그 순간으로 되돌릴
    ///      수는 없다 — 대신 "카드 데이터를 언제 다시 읽어서 보여줄지"를 큐 재생 타이밍에 맞춰
    ///      늦춰서, 눈에는 마치 그 순간 벌어지는 것처럼 보이게 만든다(재생 순서=훅이 불린 순서).
    /// [주의] 데미지/회복 숫자는 엔진이 다시 계산해주지 않는다(그런 훅이 없다). 대신 슬롯이 발동되기
    ///      직전(OnSlotActivating) 필드의 모든 캐릭터 HP/방어도를 스냅샷해두고, 다음 훅이 불렸을 때
    ///      (=이 슬롯의 효과가 이미 다 반영된 뒤) 그 스냅샷과 비교해 "무엇이 얼마나 바뀌었는지"만
    ///      읽는다. 데미지 수치를 스스로 계산하지 않는다 — 결과는 항상 엔진이 진실이다.
    /// </summary>
    public class BattleActionPlayer : MonoBehaviour
    {
        // ── 타이밍 상수 (전부 full_prototype.html 출처, 없는 것만 임의값 표기) ──────────────

        /// <summary>발동 훅~효과 반영까지 유지 시간. [출처] "slot.add('firing');await sleep(480)"</summary>
        private const float ActivationHoldBeforeMs = 480f;

        /// <summary>효과 반영~발광 해제까지 유지 시간. [출처] "resolveAct();...await sleep(400)"</summary>
        private const float ActivationHoldAfterMs = 400f;

        /// <summary>발광 켜짐/꺼짐 트윈 시간. [출처] .aslot{transition:all .2s}</summary>
        private const float FiringTweenMs = 200f;

        /// <summary>발동 슬롯 확대 배율. [주의] full_prototype .firing엔 확대가 없다(발광만) —
        /// "카드 살짝 확대" 지시에 따라 추가한 임의값.</summary>
        private const float FiringScale = 1.06f;

        /// <summary>데미지/회복 숫자 표시 시간. [출처] .dmgf.show{animation:dmg .9s ease-out}</summary>
        private const float DamageFloatDurationMs = 900f;

        /// <summary>데미지 숫자 세로 위치(카드 상단에서 %). [출처] .cchar .dmgf{top:40%}</summary>
        private const float DamageFloatTopRatio = 0.40f;

        /// <summary>데미지 숫자 폰트 크기(프로토타입 카드 폭 150px 기준). [출처] .cchar .dmgf{font-size:34px}</summary>
        private const float DamageFloatFontSizeAt150 = 34f;

        /// <summary>사망 페이드아웃 시간. [출처] .cchar{transition:...opacity .4s...}</summary>
        private const float DeathFadeMs = 400f;

        /// <summary>페이드 완료~전방 당김 시작까지 유지 시간(페이드 400ms 포함 총 500ms). [출처] endPhase "await sleep(500)"</summary>
        private const float DeathHoldMs = 500f;

        /// <summary>전방 당김(슬라이드) 시간. [출처] .cchar{transition:left .5s cubic-bezier(.2,.8,.3,1)}</summary>
        private const float PullDurationMs = 500f;

        /// <summary>당김 완료 후 정지 시간(다음 턴으로 넘어가기 전 눈에 담을 여유). [출처] endPhase "await sleep(650)"</summary>
        private const float PullSettleMs = 650f;

        // ── 2단계: 액셀 오버레이 타이밍 (전부 full_prototype.html accelProcess() 출처) ──────

        /// <summary>오버레이가 뜨고 첫 카드 공개 전까지의 긴장 대기. [출처] "log('⚡ 액셀...');await sleep(900)"</summary>
        private const float AccelTensionMs = 900f;

        /// <summary>재대결(2라운드 이상) 로그 표시 후 대기. [출처] "재대결 N라운드...await sleep(600)"</summary>
        private const float AccelRematchMs = 600f;

        /// <summary>카드가 뒤집히기 전 "뜸". [출처] "classList.remove('flip');...await sleep(700)"(A/B 공통)</summary>
        private const float AccelRevealDelayMs = 700f;

        /// <summary>카드 공개 후 유지("쫄리게"). [출처] "aWin=...;await sleep(900)"(A/B 공통)</summary>
        private const float AccelRevealHoldMs = 900f;

        /// <summary>라운드 종료~최종 판정 표시 전 대기. [출처] "await sleep(500)" (accelProcess 끝부분)</summary>
        private const float AccelResultDelayMs = 500f;

        /// <summary>최종 판정 확인 후 오버레이 닫기 전 유지. [출처] "log(선공/불발);await sleep(700)"</summary>
        private const float AccelResultHoldMs = 700f;

        // 색 — 출처: full_prototype.html --hp(#e0512f)/--reach 계열 중 데미지·회복에 쓰인 값
        private static readonly Color DamageColor = new Color32(0xFF, 0x5A, 0x3C, 0xFF); // .dmgf 기본색
        private static readonly Color HealColor = new Color32(0x5F, 0xE0, 0x8A, 0xFF);    // .dmgf.heal

        // 색 — 출처: full_prototype.html .accel-ov/.accel-top/.accel-side.win 등
        private static readonly Color AccelColor = new Color32(0x31, 0xD0, 0xF0, 0xFF);       // --accel
        private static readonly Color AccelMutedColor = new Color32(0x9A, 0xA6, 0xB8, 0xFF);  // --muted
        private static readonly Color AccelResultColor = new Color32(0xF2, 0xC9, 0x34, 0xFF); // --cost(기존 PhasePanel ResultText와 같은 톤)
        private static readonly Color AccelOverlayBg = new Color32(0x04, 0x07, 0x0C, 0xCC);   // rgba(4,7,12,.8)
        private static readonly Color AccelCardBg = new Color32(0x16, 0x1D, 0x30, 0xFF);      // --card-mid(그라데이션 단색화, CardHoverPreview와 동일 근사)
        private static readonly Color AccelBorder = new Color(1f, 1f, 1f, 0.14f);             // --line

        // ── 의존 참조 (Initialize에서 주입) ───────────────────────────────────────
        private CharacterCardView[] _myCards;
        private CharacterCardView[] _opponentCards;
        private ActSlotView[] _myActSlots;
        private ActSlotView[] _opponentActSlots;
        private GameManager _game;
        private int _myPlayerId;
        private int _enemyPlayerId;

        /// <summary>카드 슬롯 8칸(내0~3, 상대0~3)의 "제자리" 화면 좌표 — 당김 애니메이션의 시작/끝점.</summary>
        private Vector2[] _myHome;
        private Vector2[] _opponentHome;

        /// <summary>프로토타입 카드(150px) 대비 실제 배틀 카드 폭 비율 — 픽셀 단위 상수(폰트·오프셋)에 곱한다.</summary>
        private float _cardScale = 1f;

        // ── 큐 ────────────────────────────────────────────────────────────────
        private readonly Queue<object> _queue = new Queue<object>();

        /// <summary>지금 발동~결과 확정을 기다리는 중인 슬롯 이벤트(다음 훅이 와야 델타를 확정할 수 있다).</summary>
        private ActivationBeat _pendingActivation;
        private Dictionary<CharacterUnit, (int hp, int def)> _pendingSnapshot;

        /// <summary>이번 액션 페이즈에서 모은 사망 묶음(엔드 페이즈 하나당 한 묶음 — 여러 캐릭터가 동시에 죽을 수 있다).</summary>
        private DeathBatch _pendingDeathBatch;

        /// <summary>액션 페이즈 시작 시점의 캐릭터 존 순서 — 당김 애니메이션이 "원래 자리"를 알아야 한다.</summary>
        private List<CharacterUnit> _prePhaseMyOrder;
        private List<CharacterUnit> _prePhaseEnemyOrder;

        // ── 2단계: 액셀 오버레이 (처음 재생될 때 스스로 만든다 — Ensure* 패턴) ─────────────
        private GameObject _accelOverlay;
        private Text _accelTitleText;
        private Text _accelSubtitleText;
        private Text _accelLabelA;
        private Text _accelLabelB;
        private Text _accelCardA;
        private Text _accelCardB;
        private Image _accelCardBoxA;
        private Image _accelCardBoxB;
        private Text _accelReqA;
        private Text _accelReqB;
        private Text _accelResultText;

        /// <summary>슬롯 1건의 발동 연출 — 발광+확대로 시작해서, 그 사이 확정된 데미지/회복을 보여주고 끝난다.</summary>
        private class ActivationBeat
        {
            public ActiveSlot Slot;
            public readonly List<DamageResult> Results = new List<DamageResult>();
        }

        private struct DamageResult
        {
            public CharacterUnit Unit;
            public int Delta; // 양수=회복, 음수=데미지. 0이면 애초에 큐에 안 넣는다(방어도만 깎이고 HP 불변 등).
        }

        /// <summary>엔드 페이즈 1건의 사망 묶음 — 동시에 페이드아웃하고, 한 번에 전방으로 당긴다.</summary>
        private class DeathBatch
        {
            public readonly List<CharacterUnit> Units = new List<CharacterUnit>();
        }

        /// <summary>
        /// 액셀 챌린지 1건 — 엔진이 남긴 <see cref="ChallengeRecord.Rounds"/>를 순서대로 재생한다.
        /// [주의] 라운드 데이터를 여기서 따로 들고 있지 않는다. 예전엔 UI가 트래쉬 증가분으로 라운드를
        ///        역산해 보관했는데(부수효과 때문에 깨졌다), 이제 엔진이 직접 기록하므로 Record만
        ///        들고 있으면 된다 — UI가 가진 사본이 엔진과 어긋날 여지가 아예 없어졌다.
        /// </summary>
        private class ChallengeBeat
        {
            public ChallengeRecord Record;
        }

        // ===================== 초기화 =====================

        /// <summary>
        /// [무엇] 재생에 필요한 뷰 참조와 엔진을 주입한다. BattleUIController가 한 번만 호출한다.
        /// [왜] 이 클래스는 자기 손으로 뷰를 찾지 않는다(중복 탐색·불일치 방지) — Awake에서 이미
        ///      찾아둔 배열을 그대로 공유받는다.
        /// </summary>
        public void Initialize(CharacterCardView[] myCards, CharacterCardView[] opponentCards,
            ActSlotView[] myActSlots, ActSlotView[] opponentActSlots,
            GameManager game, int myPlayerId, int enemyPlayerId)
        {
            _myCards = myCards;
            _opponentCards = opponentCards;
            _myActSlots = myActSlots;
            _opponentActSlots = opponentActSlots;
            _game = game;
            _myPlayerId = myPlayerId;
            _enemyPlayerId = enemyPlayerId;

            _myHome = CaptureHome(_myCards);
            _opponentHome = CaptureHome(_opponentCards);

            // 프로토타입은 캐릭터 카드 150px을 기준으로 픽셀값을 정했다. 지금 배틀 카드가 그보다
            // 작으면(현재 126px) 비례 축소해야 데미지 숫자가 카드에 비해 과하게 커 보이지 않는다.
            if (_myCards.Length > 0 && _myCards[0] != null)
            {
                float actualWidth = ((RectTransform)_myCards[0].transform).rect.width;
                if (actualWidth > 0f) _cardScale = actualWidth / 150f;
            }
        }

        private static Vector2[] CaptureHome(CharacterCardView[] cards)
        {
            var home = new Vector2[cards.Length];
            for (int i = 0; i < cards.Length; i++)
                home[i] = cards[i] != null ? ((RectTransform)cards[i].transform).anchoredPosition : Vector2.zero;
            return home;
        }

        // ===================== 훅 → 큐 적재 (BattleUIController가 호출) =====================

        /// <summary>
        /// [무엇] 액션 페이즈 시작 직전에 호출한다(엔진의 RunActionPhase보다 먼저).
        /// [왜] 당김 애니메이션은 "원래 어디 있었는지"를 알아야 한다. RunActionPhase가 끝나면
        ///      이미 캐릭터 존이 앞당겨진 뒤라 그 순서를 여기서 미리 찍어둬야 한다.
        /// </summary>
        public void BeginActionPhase()
        {
            _prePhaseMyOrder = new List<CharacterUnit>(_game.Players[_myPlayerId].CharacterZone);
            _prePhaseEnemyOrder = new List<CharacterUnit>(_game.Players[_enemyPlayerId].CharacterZone);
            _pendingActivation = null;
            _pendingSnapshot = null;
            _pendingDeathBatch = null;

            // 지난 액션 페이즈에서 죽은 캐릭터의 카드가 알파 0으로 남아있을 수 있다 — 다음에 그
            // 자리를 다른 캐릭터가 쓸 때 안 보이는 사고를 막기 위해 매 페이즈 시작 시 초기화한다.
            ResetAlpha(_myCards);
            ResetAlpha(_opponentCards);
        }

        /// <summary>[무엇] 카드 한 장이 발동되기 직전 훅을 큐에 적재한다.</summary>
        public void EnqueueSlotActivating(ActiveSlot slot)
        {
            FlushPendingDamage(); // 직전 슬롯의 결과를 지금 상태와 비교해 확정한다
            var beat = new ActivationBeat { Slot = slot };
            _queue.Enqueue(beat);
            _pendingActivation = beat;
            _pendingSnapshot = SnapshotAll();
        }

        /// <summary>
        /// [무엇] 액셀 판정 훅을 큐에 적재한다.
        /// [왜] 라운드별 덱탑 카드는 엔진이 <see cref="ChallengeRecord.Rounds"/>에 직접 기록해 주므로
        ///      여기서는 그 record를 그대로 들고만 있으면 된다. 예전에는 이 기록이 없어서 UI가 트래쉬
        ///      증가분으로 라운드를 역산했는데, 스킬 부수효과가 트래쉬에 카드를 더 버리면 어긋나
        ///      깨졌다 — 그걸 UI에서 흉내 내면 판정 재계산이 되므로 엔진에 기록을 요청해 해결했다.
        /// [주의] 챌린지 자체는 데미지를 만들지 않지만, 직전 슬롯의 데미지 확정은 그대로 필요하다
        ///        (같은 속도에 챌린지가 연달아 걸리면 그 사이엔 OnSlotActivating이 없다).
        /// </summary>
        public void EnqueueChallengeResolved(ChallengeRecord record)
        {
            FlushPendingDamage();
            _queue.Enqueue(new ChallengeBeat { Record = record });
        }

        /// <summary>[무엇] 사망 훅을 이번 엔드 페이즈의 묶음에 담는다(여러 명이 한 번에 죽을 수 있다).</summary>
        public void EnqueueCharacterDied(CharacterUnit unit)
        {
            if (_pendingDeathBatch == null)
            {
                _pendingDeathBatch = new DeathBatch();
                _queue.Enqueue(_pendingDeathBatch);
            }
            _pendingDeathBatch.Units.Add(unit);
        }

        /// <summary>
        /// [무엇] RunActionPhase가 반환한 직후 호출한다.
        /// [왜] 마지막 슬롯의 데미지는 그 뒤로 아무 훅도 안 불릴 수 있다(사망이 없으면 특히) —
        ///      다음 훅을 기다리지 말고 여기서 강제로 확정해야 마지막 한 건이 안 빠진다.
        /// </summary>
        public void EndActionPhase()
        {
            FlushPendingDamage();
            _pendingDeathBatch = null;
        }

        /// <summary>스냅샷과 현재 상태를 비교해 델타가 있는 유닛만 직전 ActivationBeat에 담는다.</summary>
        private void FlushPendingDamage()
        {
            if (_pendingActivation == null || _pendingSnapshot == null) return;

            foreach (var kv in _pendingSnapshot)
            {
                int delta = kv.Key.CurrentHp - kv.Value.hp;
                if (delta == 0) continue; // 방어도만 깎였거나 변화 없음 — 띄울 게 없다
                _pendingActivation.Results.Add(new DamageResult { Unit = kv.Key, Delta = delta });
            }

            _pendingActivation = null;
            _pendingSnapshot = null;
        }

        private Dictionary<CharacterUnit, (int, int)> SnapshotAll()
        {
            var snap = new Dictionary<CharacterUnit, (int, int)>();
            foreach (var u in _game.Players[_myPlayerId].CharacterZone) snap[u] = (u.CurrentHp, u.Defense);
            foreach (var u in _game.Players[_enemyPlayerId].CharacterZone) snap[u] = (u.CurrentHp, u.Defense);
            return snap;
        }

        // ===================== 재생 =====================

        /// <summary>
        /// [무엇] 큐를 순서대로 재생하고 끝나면 onComplete를 부른다.
        /// [왜] BattleUIController가 "재생 다 끝난 뒤에" RefreshAll()로 최종 상태를 한 번 더
        ///      동기화한다 — 애니메이션 중 자잘하게 어긋난 게 있어도 마지막에 항상 바로잡힌다.
        /// </summary>
        public void Play(System.Action onComplete)
        {
            StartCoroutine(PlayRoutine(onComplete));
        }

        private IEnumerator PlayRoutine(System.Action onComplete)
        {
            while (_queue.Count > 0)
            {
                var beat = _queue.Dequeue();
                if (beat is ActivationBeat activation) yield return PlayActivation(activation);
                else if (beat is DeathBatch deaths) yield return PlayDeathBatch(deaths);
                else if (beat is ChallengeBeat challenge) yield return PlayChallenge(challenge);
            }
            onComplete?.Invoke();
        }

        // ── 발동 연출 ────────────────────────────────────────────────────────

        private IEnumerator PlayActivation(ActivationBeat beat)
        {
            var slotView = FindActSlot(beat.Slot);
            RectTransform slotRt = slotView != null ? (RectTransform)slotView.transform : null;

            slotView?.SetFiring(true);
            if (slotRt != null) yield return ScaleTo(slotRt, FiringScale, FiringTweenMs);

            yield return WaitMs(ActivationHoldBeforeMs);

            foreach (var result in beat.Results)
            {
                var card = FindCard(result.Unit);
                if (card == null) continue;
                card.Refresh(); // 엔진이 이미 바꿔놓은 CurrentHp를 다시 읽어 숫자만 갱신 (재계산 아님)
                yield return SpawnDamageFloat(card, result.Delta);
            }

            yield return WaitMs(ActivationHoldAfterMs);

            if (slotRt != null) yield return ScaleTo(slotRt, 1f, FiringTweenMs);
            slotView?.SetFiring(false);
        }

        /// <summary>발동한 슬롯이 화면 어느 ActSlotView인지 찾는다. ActiveZone 순서=슬롯 배열 인덱스.</summary>
        private ActSlotView FindActSlot(ActiveSlot slot)
        {
            var owner = _game.Players[slot.OwnerId];
            int index = owner.ActiveZone.IndexOf(slot);
            if (index < 0) return null;

            var slots = slot.OwnerId == _myPlayerId ? _myActSlots : _opponentActSlots;
            return index < slots.Length ? slots[index] : null;
        }

        /// <summary>지금 이 유닛을 표시 중인 카드를 찾는다. 재생 도중엔 아직 재바인딩을 안 했으므로 참조가 유지된다.</summary>
        private CharacterCardView FindCard(CharacterUnit unit)
        {
            return System.Array.Find(_myCards, c => c != null && c.Unit == unit)
                   ?? System.Array.Find(_opponentCards, c => c != null && c.Unit == unit);
        }

        /// <summary>데미지/회복 숫자를 카드 위에 띄운다. 스폰 즉시 반환하지 않고 재생이 끝날 때까지 기다린다.</summary>
        private IEnumerator SpawnDamageFloat(CharacterCardView card, int delta)
        {
            var go = new GameObject("DamageFloat", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(card.transform, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f - DamageFloatTopRatio);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(220f, 60f);

            var text = go.AddComponent<Text>();
            text.font = GameUIFont.Legacy; // 카드 내용이 아니라 연출용 오버레이라 게임 UI 폰트를 쓴다
            text.fontSize = Mathf.RoundToInt(DamageFloatFontSizeAt150 * _cardScale);
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.text = (delta > 0 ? "+" : "") + delta; // delta는 엔진이 실제로 바꾼 HP 증감분 그대로
            text.color = delta > 0 ? HealColor : DamageColor;

            float duration = DamageFloatDurationMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                var (alpha, y, scale) = EvaluateDamageFloatKeyframe(t);
                var c = text.color; c.a = alpha; text.color = c;
                rt.anchoredPosition = new Vector2(0f, y * _cardScale);
                rt.localScale = Vector3.one * scale;
                yield return null;
            }

            Destroy(go);
        }

        /// <summary>[출처] full_prototype.html @keyframes dmg — 0/20/80/100% 4구간을 구간별 선형보간한다.</summary>
        private static (float alpha, float y, float scale) EvaluateDamageFloatKeyframe(float t)
        {
            if (t < 0.2f)
            {
                float k = t / 0.2f;
                return (Mathf.Lerp(0f, 1f, k), Mathf.Lerp(6f, -4f, k), Mathf.Lerp(0.7f, 1.15f, k));
            }
            if (t < 0.8f)
            {
                return (1f, -4f, 1.15f); // 20%~80% 구간은 CSS에 재지정이 없어 20% 값을 유지
            }
            float k2 = (t - 0.8f) / 0.2f;
            return (Mathf.Lerp(1f, 0f, k2), Mathf.Lerp(-4f, -26f, k2), 1.15f);
        }

        // ── 사망 + 전방 당김 연출 ────────────────────────────────────────────

        private IEnumerator PlayDeathBatch(DeathBatch batch)
        {
            var dyingCards = batch.Units.Select(FindCard).Where(c => c != null).ToList();

            yield return FadeOutAll(dyingCards, DeathFadeMs);
            yield return WaitMs(DeathHoldMs - DeathFadeMs); // 총 500ms 중 페이드(400ms)를 뺀 나머지만 더 기다린다

            yield return RunParallel(
                PullSide(_myCards, _prePhaseMyOrder, _game.Players[_myPlayerId].CharacterZone, _myHome),
                PullSide(_opponentCards, _prePhaseEnemyOrder, _game.Players[_enemyPlayerId].CharacterZone, _opponentHome));

            yield return WaitMs(PullSettleMs);
        }

        private IEnumerator FadeOutAll(List<CharacterCardView> cards, float durationMs)
        {
            if (cards.Count == 0) yield break;

            var groups = cards.Select(GetOrAddCanvasGroup).ToList();
            float duration = durationMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration);
                foreach (var g in groups) g.alpha = alpha;
                yield return null;
            }
            foreach (var g in groups) g.alpha = 0f;
        }

        /// <summary>
        /// [무엇] 한쪽 진영의 생존자를 "원래 있던 화면 위치"에서 "당겨진 새 위치"로 슬라이드시킨다.
        /// [왜] 카드 칸(MyCharacter0~3)은 화면상 고정된 자리라 데이터만 다시 꽂으면 순간이동처럼
        ///      보인다. 그래서 새 칸에 그 유닛 데이터를 먼저 앉히고, 화면 위치만 "이전 칸의 자리"에서
        ///      시작해 "새 칸의 자리"로 트윈한다 — 카드 오브젝트 자체가 아니라 자리(칸)가 고정이므로
        ///      나온 결과물은 "카드가 이동하는" 것처럼 보인다.
        /// [주의] R9 앞당김은 순서를 보존하므로(뒤 캐릭터가 앞으로 당겨질 뿐 순번이 안 바뀐다)
        ///        old→new 인덱스가 줄어드는 방향으로만 이동한다.
        /// </summary>
        private IEnumerator PullSide(CharacterCardView[] cards, List<CharacterUnit> preOrder,
            IReadOnlyList<CharacterUnit> finalOrder, Vector2[] home)
        {
            var moves = new List<IEnumerator>();

            for (int newIndex = 0; newIndex < finalOrder.Count; newIndex++)
            {
                var unit = finalOrder[newIndex];
                int oldIndex = preOrder.IndexOf(unit);
                if (oldIndex < 0 || oldIndex == newIndex || cards[newIndex] == null) continue;

                var card = cards[newIndex];
                card.Bind(unit);
                GetOrAddCanvasGroup(card).alpha = 1f;
                var rt = (RectTransform)card.transform;
                rt.anchoredPosition = home[oldIndex]; // 원래 자리에서 시작
                moves.Add(MoveTo(rt, home[newIndex], PullDurationMs));
            }

            // 죽은 캐릭터가 있던 자리 중 아무도 안 옮겨온 뒤쪽 칸은 바로 비운다(잔상 방지).
            for (int i = finalOrder.Count; i < cards.Length; i++)
                cards[i]?.SetEmpty();

            yield return RunParallel(moves.ToArray());
        }

        private static CanvasGroup GetOrAddCanvasGroup(CharacterCardView card)
        {
            var group = card.GetComponent<CanvasGroup>();
            if (group == null) group = card.gameObject.AddComponent<CanvasGroup>();
            return group;
        }

        private static void ResetAlpha(CharacterCardView[] cards)
        {
            foreach (var card in cards)
            {
                if (card == null) continue;
                var group = card.GetComponent<CanvasGroup>();
                if (group != null) group.alpha = 1f;
            }
        }

        // ── 2단계: 액셀 오버레이 ─────────────────────────────────────────────

        /// <summary>
        /// [무엇] 챌린지 1건을 full_prototype.html accelProcess()와 같은 순서·시간으로 재생한다.
        /// [주의] 라운드 수·공개 무기·성공 여부·승자 전부 엔진이 기록한 <see cref="ChallengeRecord"/>
        ///        값을 그대로 읽는다 — 여기서는 그걸 순서대로 "느리게" 보여주기만 하고, 아무것도
        ///        다시 판정하지 않는다(UI는 엔진 값을 표시만 한다 — UNITY_PORTING_SPEC 1절 4번).
        /// [주의] 오버레이는 전체화면 모달이라 다른 UI와 겹쳐도 된다(사용자 승인) — 재생이 끝나면
        ///        SetActive(false)로 완전히 사라지므로 이후 화면엔 흔적이 안 남는다.
        /// </summary>
        private IEnumerator PlayChallenge(ChallengeBeat beat)
        {
            EnsureAccelOverlay();
            var record = beat.Record;
            var rounds = record.Rounds;

            string nameA = record.Slot0.User?.Data.Name ?? "?";
            string nameB = record.Slot1.User?.Data.Name ?? "?";
            string sideA = record.Slot0.OwnerId == _myPlayerId ? "나" : "상대";
            string sideB = record.Slot1.OwnerId == _myPlayerId ? "나" : "상대";

            _accelOverlay.SetActive(true);
            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: "⚡ ACCEL ⚡")
            _accelTitleText.text = "ACCEL";
            _accelSubtitleText.text = $"속도 {record.Slot0.Skill.Speed} 동점";
            _accelLabelA.text = $"{sideA} · {nameA}";
            _accelLabelB.text = $"{sideB} · {nameB}";
            _accelReqA.text = $"필요 무기: {record.Slot0.User?.Data.WeaponType}";
            _accelReqB.text = $"필요 무기: {record.Slot1.User?.Data.WeaponType}";
            _accelResultText.text = "";
            ResetAccelRound();

            yield return WaitMs(AccelTensionMs);

            for (int r = 0; r < rounds.Count; r++)
            {
                var round = rounds[r];

                if (r > 0)
                {
                    // 재대결은 "직전 라운드에 양쪽 다 성공"했을 때만 일어난다(엔진 불변식) —
                    // 그래서 2라운드 이상이 기록돼 있다는 것 자체가 재대결이 있었다는 뜻이다.
                    _accelSubtitleText.text = $"둘 다 일치 — 재대결 {r + 1}라운드";
                    yield return WaitMs(AccelRematchMs);
                    ResetAccelRound();
                }

                yield return WaitMs(AccelRevealDelayMs); // A 뜸
                _accelCardA.text = DeckTopLabel(round.WeaponA);
                SetAccelWin(_accelCardBoxA, _accelLabelA, round.SuccessA);
                yield return WaitMs(AccelRevealHoldMs);

                yield return WaitMs(AccelRevealDelayMs); // B 뜸
                _accelCardB.text = DeckTopLabel(round.WeaponB);
                SetAccelWin(_accelCardBoxB, _accelLabelB, round.SuccessB);
                yield return WaitMs(AccelRevealHoldMs);
            }

            yield return WaitMs(AccelResultDelayMs);

            // TODO: 게임 UI 폰트 교체 시 이모지 복원 (원래: 선공 문구 앞에 "⚡ ")
            if (record.Winner == GameManager.NoWinner)
                _accelResultText.text = "→ 무승부, 원래 순서대로 진행";
            else if (record.Winner == record.Slot0.OwnerId)
                _accelResultText.text = $"{nameA} 선공! {nameB} 불발";
            else
                _accelResultText.text = $"{nameB} 선공! {nameA} 불발";

            yield return WaitMs(AccelResultHoldMs);

            _accelOverlay.SetActive(false); // 모달을 끄면 그 아래 화면은 그대로 원복(따로 되돌릴 상태가 없다)
        }

        private void ResetAccelRound()
        {
            _accelCardA.text = "?";
            _accelCardB.text = "?";
            SetAccelWin(_accelCardBoxA, _accelLabelA, false);
            SetAccelWin(_accelCardBoxB, _accelLabelB, false);
        }

        /// <summary>
        /// [무엇] 공개된 덱탑 카드의 표시 문구.
        /// [왜] 엔진 기록에서 무기가 null이면 "그 쪽 덱이 말라 공개조차 못 했다"는 뜻이다
        ///      (ChallengeRound 주석 / RULES.md 8-5·8-6). 그 경우 무기 이름 대신 사유를 보여줘야
        ///      "왜 졌는지"가 화면에서 이해된다.
        /// </summary>
        private static string DeckTopLabel(string weapon) => weapon ?? "덱 소진";

        /// <summary>[출처] full_prototype.html .accel-side.win — 성공한 쪽 카드 테두리 발광 + 라벨 강조.</summary>
        private static void SetAccelWin(Image box, Text label, bool win)
        {
            var outline = box.GetComponent<Outline>();
            outline.effectColor = win ? AccelColor : Color.clear;
            label.color = win ? AccelColor : AccelMutedColor;
        }

        /// <summary>
        /// [무엇] 액셀 오버레이 UI를 처음 재생할 때 한 번만 만든다(이후엔 SetActive로 켜고 끈다).
        /// [출처 좌표] full_prototype.html .accel-ov(전체화면)/.accel-box/.at(제목)/.accel-side/
        ///        .accel-top(카드 120×170)/.accel-vs — flex 레이아웃이라 절대좌표가 없어, 1920×1080
        ///        캔버스에 같은 비례로 직접 배치하고 계산으로 겹침을 검산했다(아래 각 요소 주석).
        /// </summary>
        private void EnsureAccelOverlay()
        {
            if (_accelOverlay != null) return;

            var canvasRoot = (RectTransform)transform;

            // 전체화면 반투명 백드롭 — 다른 모든 UI 위(마지막 자식이라 항상 최상단).
            var overlayRt = CreateUIRect(canvasRoot, "AccelOverlay", 0, 0, 1920, 1080);
            var overlayImage = overlayRt.gameObject.AddComponent<Image>();
            overlayImage.color = AccelOverlayBg;
            overlayImage.raycastTarget = true; // 뒤 UI 클릭 차단(모달)
            _accelOverlay = overlayRt.gameObject;

            // 제목 "⚡ ACCEL ⚡" — top 280, 가로 중앙
            _accelTitleText = CreateAccelLabel(overlayRt, "Title", 560, 280, 800, 60, 40, AccelColor, FontStyle.Bold);
            // 부제(속도 동점/재대결 안내) — top 344
            _accelSubtitleText = CreateAccelLabel(overlayRt, "Subtitle", 560, 344, 800, 28, 18, AccelMutedColor, FontStyle.Normal);

            // 카드 열: A 중심 x=740, VS 중심 x=960, B 중심 x=1180 (검산은 완료 보고 표 참고)
            const float colTop = 420f, labelH = 24f, gap = 10f, cardW = 120f, cardH = 170f;
            float cardTop = colTop + labelH + gap;               // 454
            float reqTop = cardTop + cardH + 8f;                 // 632

            _accelLabelA = CreateAccelLabel(overlayRt, "LabelA", 600, colTop, 280, labelH, 14, AccelMutedColor, FontStyle.Bold);
            (_accelCardBoxA, _accelCardA) = CreateAccelCard(overlayRt, "CardA", 680, cardTop, cardW, cardH);
            _accelReqA = CreateAccelLabel(overlayRt, "ReqA", 620, reqTop, 240, 20, 12, AccelMutedColor, FontStyle.Normal);

            CreateAccelLabel(overlayRt, "VS", 930, cardTop + cardH / 2f - 20f, 60, 40, 30, AccelMutedColor, FontStyle.Bold).text = "VS";

            _accelLabelB = CreateAccelLabel(overlayRt, "LabelB", 1040, colTop, 280, labelH, 14, AccelMutedColor, FontStyle.Bold);
            (_accelCardBoxB, _accelCardB) = CreateAccelCard(overlayRt, "CardB", 1120, cardTop, cardW, cardH);
            _accelReqB = CreateAccelLabel(overlayRt, "ReqB", 1060, reqTop, 240, 20, 12, AccelMutedColor, FontStyle.Normal);

            // 최종 판정 — top 700 (ReqText 하단 652에서 48px 여백)
            _accelResultText = CreateAccelLabel(overlayRt, "Result", 560, 700, 800, 50, 26, AccelResultColor, FontStyle.Bold);

            _accelOverlay.SetActive(false);
        }

        private static (Image box, Text value) CreateAccelCard(RectTransform parent, string name, float left, float top, float width, float height)
        {
            var rt = CreateUIRect(parent, name, left, top, width, height);
            var box = rt.gameObject.AddComponent<Image>();
            box.color = AccelCardBg;
            box.raycastTarget = false;

            // Outline을 2개 겹친다 — 첫 번째(윤곽 두꺼운 쪽)가 SetAccelWin이 토글하는 성공 발광이고,
            // 두 번째(얇은 쪽)는 항상 켜진 정적 테두리(--line 근사)다. GetComponent<Outline>()은
            // 항상 먼저 붙인 것을 돌려주므로 SetAccelWin은 매번 발광 쪽만 정확히 잡는다.
            var glow = rt.gameObject.AddComponent<Outline>();
            glow.effectColor = Color.clear;
            glow.effectDistance = new Vector2(3f, 3f);

            var border = rt.gameObject.AddComponent<Outline>();
            border.effectColor = AccelBorder;
            border.effectDistance = new Vector2(1.5f, 1.5f);

            var value = CreateAccelLabel(rt, "Value", 0, 0, width, height, 22, Color.white, FontStyle.Bold);
            value.text = "?";
            return (box, value);
        }

        private static Text CreateAccelLabel(RectTransform parent, string name, float left, float top,
            float width, float height, int fontSize, Color color, FontStyle style)
        {
            var rt = CreateUIRect(parent, name, left, top, width, height);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = GameUIFont.Legacy; // 카드 내용이 아니라 연출 오버레이 — 게임 UI 폰트
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateUIRect(Transform parent, string name, float left, float top, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        // ── 공용 트윈 헬퍼 ───────────────────────────────────────────────────

        private static IEnumerator WaitMs(float ms)
        {
            if (ms <= 0f) yield break;
            yield return new WaitForSeconds(ms / 1000f);
        }

        private static IEnumerator ScaleTo(RectTransform rt, float targetScale, float durationMs)
        {
            Vector3 start = rt.localScale;
            Vector3 target = Vector3.one * targetScale;
            float duration = durationMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                rt.localScale = Vector3.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            rt.localScale = target;
        }

        private static IEnumerator MoveTo(RectTransform rt, Vector2 target, float durationMs)
        {
            Vector2 start = rt.anchoredPosition;
            float duration = durationMs / 1000f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = CubicBezierEase(t, 0.2f, 0.8f, 0.3f, 1f); // .cchar left transition 이징
                rt.anchoredPosition = Vector2.Lerp(start, target, eased);
                yield return null;
            }
            rt.anchoredPosition = target;
        }

        /// <summary>여러 코루틴을 동시에 실행하고 전부 끝날 때까지 기다린다(당김은 두 진영이 같이 움직여야 한다).</summary>
        private IEnumerator RunParallel(params IEnumerator[] routines)
        {
            var handles = new List<Coroutine>();
            foreach (var r in routines)
                if (r != null) handles.Add(StartCoroutine(r));
            foreach (var h in handles) yield return h;
        }

        /// <summary>
        /// CSS cubic-bezier(x1,y1,x2,y2) 근사 평가(뉴턴법). [출처] full_prototype.html .cchar
        /// "transition:left .5s cubic-bezier(.2,.8,.3,1)" — 유니티엔 CSS 이징이 없어 직접 계산한다.
        /// </summary>
        private static float CubicBezierEase(float t, float x1, float y1, float x2, float y2)
        {
            float u = t;
            for (int i = 0; i < 6; i++)
            {
                float x = BezierComponent(u, x1, x2) - t;
                float dx = BezierDerivative(u, x1, x2);
                if (Mathf.Abs(dx) < 1e-6f) break;
                u = Mathf.Clamp01(u - x / dx);
            }
            return BezierComponent(u, y1, y2);
        }

        private static float BezierComponent(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u;
        }

        private static float BezierDerivative(float u, float p1, float p2)
        {
            float v = 1f - u;
            return 3f * v * v * p1 + 6f * v * u * (p2 - p1) + 3f * u * u * (1f - p2);
        }
    }
}
