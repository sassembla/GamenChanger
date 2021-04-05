using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    /*
        フリック可能なCorner
        // TODO: Editor上で、対象となるhandlerやcornerを見つけてinspectorに表示不可だがクリックすると光るボタンとして表示したい感じある。
        // TODO: flick不可能な方向へのフリックの結果発生する、遷移できないドラッグへとバネを実装したい。まあどうやんのっていう話ではあるが
        // TODO: タッチ入力による加速度のバーチャライズをしたい。加速によってフリックを達成させるという感じ。手始めにデルタの加速度を計測してみるか。
    */
    public class FlickableCorner : Corner, IDragHandler, IEndDragHandler, IPointerEnterHandler
    {
        private enum FlickAnimationState
        {
            NONE,
            PROCESSING,
            CANCELLING,
        }
        private FlickAnimationState animationState = FlickAnimationState.NONE;

        // このCornerのデフォルト位置
        private Vector2 initalPos;


        // flickが発生してる場合trueを返す。
        internal bool HasActiveFlick()
        {
            return flickIdentity != null;
        }

        // アニメーション中な場合trueを返す。
        internal bool IsAnimating()
        {
            return animationState != FlickAnimationState.NONE;
        }

        internal void InactivateCurrentFlickIdentity()
        {
            FrameLog("InactivateCurrentFlickIdentityって言われた状態、さてどうなる？");
            flickIdentity = null;
        }

        // このFlickableCornerと、さらに同一レイヤーに存在する全てのFlickableCornerのイベントを受け取るハンドラ。
        // 別に単に切り替わる画面が欲しい場合もあるため、参照が存在しない場合も許容する。
        private IFlickableCornerHandler handler;

        // このアプリケーション上に存在する全てのFlickableCornerが所属するネットワーク、、とか、そういうの、、いやーーうーんん、、
        // 単になんかタッチが跨いでも問題なければいいだけなんだよな、、それって簡単に解決できないかな、、フォーカスの概念があればいけるか？
        // private static Dictionary<int, FlickableCornerNetwork> network = new Dictionary<int, FlickableCornerNetwork>();
        // private static List<> なんかsharedなタッチの情報をここに乗っけておいて、探索して他にもいれば、とかができる気はするが、
        // そもそも付け焼き刃でやらない方がいい気はしている。あとでやろう。

        // このFlickableCornerに対して発生しているflickの一意なインスタンスが持つ要素。
        // 複数のタッチが継続的に動作するのを、一つのflick操作として認識するために生成される。

        // 収集くんを流すのが良さそう、そんでそれが同期的に流れれば良さそう。
        private class FlickIdentity
        {
            private int currentTouchId = -2;
            public readonly FlickDirection interactableDir;
            public readonly FlickDirection flickableDir;

            public FlickIdentity(int initialTouchId, FlickDirection interactableFlickDir, FlickDirection flickableDir)
            {
                this.currentTouchId = initialTouchId;
                this.interactableDir = interactableFlickDir;
                this.flickableDir = flickableDir;
            }

            public void UpdateTouchId(int newTouchId)
            {
                this.currentTouchId = newTouchId;

                // 以前のtouchIdで稼いでいた距離の委譲を行う
                this.additionalDiff += this.diffOfCurrentTouchId;

                // リセットを行う
                this.diffOfCurrentTouchId = Vector2.zero;
            }
            public int TouchId => currentTouchId;

            private Vector2 additionalDiff = Vector2.zero;

            private Vector2 diffOfCurrentTouchId = Vector2.zero;

            public void UpdateDiff(Vector2 diffOfCurrentTouchId)
            {
                this.diffOfCurrentTouchId = diffOfCurrentTouchId;
            }

            public Vector2 InheritedDiff => additionalDiff;

            // デバッグ用
            public override string ToString()
            {
                return "currentTouchId:" + currentTouchId + " availableDir:" + interactableDir;
            }
        }

        private FlickIdentity flickIdentity;

        // この画面でdrag操作として認められ、まだendしていないタッチのidを収集してあるもの。
        private List<int> currentTouchIds = new List<int>();

        // dragで個々のタッチから更新され、updateで判定されるdelta値。
        private struct DeltaMovement
        {
            public readonly int index;
            public readonly float value;
            public readonly Vector2 initialPos;
            public readonly Vector2 currentPos;
            public DeltaMovement(int index, float value, Vector2 initialPos, Vector2 currentPos)
            {
                this.index = index;
                this.value = value;
                this.initialPos = initialPos;
                this.currentPos = currentPos;
            }
        }
        private DeltaMovement currentFrameMaxDeltaMovement = new DeltaMovement(-2, 0f, Vector2.zero, Vector2.zero);

        public new void Awake()
        {
            base.SetSubclassReloadAndExcludedAction(
                () =>
                {
                    // 親を探して、IFlickableCornerFocusHandlerを持っているオブジェクトがあったら、変更があるたびに上流に通知を流す。
                    if (transform.parent != null)
                    {
                        handler = transform.parent.GetComponent<IFlickableCornerHandler>();
                    }

                    // ネットワークの構築を行う(すでに別の連結している要素によってセットされている可能性はある)
                    if (fNetworkObject == null)
                    {
                        fNetworkObject = new FlickableCornersNetwork();
                        UpdateNetworkByAddedOrRemovedRelatedCorner(ref fNetworkObject);
                    }
                    else
                    {
                        return;
                    }
                },
                excludedGameObject =>
                {
                    // do nothing.
                }
            );

            base.Awake();
        }


        // uGUI drag handlers

        // TODO: 同一flickable上のdragに対してidをつけて、それごとに現在触っているFlickableCornerに接続しているflickablesが存在する感じになりそう。networkを登録する感じにするか。
        /*
            どこかのflickableがタッチを拾う -> ネットワーク内に他のタッチがなければ新規登録
            どこかのflickableがタッチを拾う -> ネットワーク内に他のタッチがあるので、無視。
            どこかのflickableがdragを拾う -> latest更新 + diffによるビューの移動(このへんが実際どうなってるかわからんのよな、やってみよ)

            エディタの場合はタッチIDが-1固定になっている。まあはい。値として気をつけよう。
            ・画面外から直にオブジェクトへのタッチを計測したい
            ・画面外から別のオブジェクトを経たタッチは計測したくない
        */

        public void OnPointerEnter(PointerEventData eventData)
        {
            // アニメーション中なら入力を受け付けない
            if (IsAnimating())
            {
                return;
            }

            // エディタでのみ、画面外からオブジェクトへのタッチインをサポートする。
            {
#if UNITY_EDITOR
                if (eventData.eligibleForClick && eventData.pointerEnter == this.gameObject && !touchInScreenForEditor)
                {
                    // このオブジェクトを強制的にdrag中にする。
                    // 弊害としてエディタで画面外からドラッグし画面内で離した場合、本来動かない側にキャンセルモーションが発生することがある。
                    eventData.pointerDrag = this.gameObject;
                    return;
                }
#endif
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // アニメーション中なら入力を受け付けない
            if (IsAnimating())
            {
                return;
            }

            // まだflick生成 = flick開始前であれば、判定を行う。
            if (flickIdentity == null)
            {
                var delta = eventData.delta;

                // 上位のハンドラがあれば、そもそもflickするか、これからflickしそうな方向へとCornerを追加するか、リクエストを送る
                if (handler != null)
                {
                    // 値から該当しそうなフリック方向を推定する
                    var estimatedFlickDir = EstimateFlickDir(delta);

                    var currentReferences = new string[] { CornerFromLeft?.CornerId, CornerFromRight?.CornerId, CornerFromTop?.CornerId, CornerFromBottom?.CornerId };

                    var isAccepted = handler.OnFlickRequestFromFlickableCorner(this, ref CornerFromLeft, ref CornerFromRight, ref CornerFromTop, ref CornerFromBottom, estimatedFlickDir);

                    // ハンドラが受け付けなければ動かない
                    if (!isAccepted)
                    {
                        return;
                    }

                    // ここで参照が変更されている可能性がある。
                    // 参照が変更されていたりしたら、networkを再構築する。
                    if (currentReferences[0] == CornerFromLeft?.CornerId && currentReferences[1] == CornerFromRight?.CornerId && currentReferences[2] == CornerFromTop?.CornerId && currentReferences[3] == CornerFromBottom?.CornerId)
                    {
                        // pass.
                    }
                    else
                    {
                        // 変更が検知できたので実行する。
                        UpdateNetworkByAddedOrRemovedRelatedCorner(ref fNetworkObject);
                    }
                }

                // 利用可能なフリック方向を確定させる(左右 or 上下 or NONE)
                var interactableFlickDir = GetInteractableDirection(delta);

                // 利用可能なflickDirを見出せなかったので終了
                if (interactableFlickDir == FlickDirection.NONE)
                {
                    return;
                }

                // flickを開始したことを他のFlickableCornerに伝える
                var approved = fNetworkObject.FlickInitializeRequest(this.CornerId, eventData.pointerId);

                // 許可されなかったので終了
                if (!approved)
                {
                    return;
                }

                // flickを初期化する。
                var flickableDir = GetFlickableDirection(interactableFlickDir);
                flickIdentity = new FlickIdentity(eventData.pointerId, interactableFlickDir, flickableDir);

                // 初期位置を規定して差分で移動させる準備をする。
                UpdateInitialPos();
            }

            // エディタの場合のみ、画面外へのフリックが継続的に生きる。
            // そのため、強制的に終了させてデバッグ効率を上げる。
            {
#if UNITY_EDITOR
                if (0 <= currentFrameMaxDeltaMovement.currentPos.x &&
                    currentFrameMaxDeltaMovement.currentPos.x < Screen.width &&
                    0 <= currentFrameMaxDeltaMovement.currentPos.y &&
                    currentFrameMaxDeltaMovement.currentPos.y < Screen.height
                )
                {
                    // 画面内なのでこのタッチは継続している
                }
                else
                {
                    // 画面外に出たので終了させる
                    // TODO: どうやるといいんだろう、、
                }
#endif
            }

            // このフレームで動いたすべてのdragの差分の中で、最大のものを収集する。
            // deltaの比較を行う。
            var deltaOfThisPointerDrag = eventData.delta;
            var dirConstrainedDeltaMovementOfThisPointerDrag = GetConstraintedDeltaMovement(flickIdentity.interactableDir, deltaOfThisPointerDrag);

            // deltaの値が大きい場合、採用する。
            // TODO: ここに一定以上のサイズだったら〜とかを足すと良さそう。微動を拾わないで済む。
            var absDelta = Mathf.Abs(dirConstrainedDeltaMovementOfThisPointerDrag);
            if (0 < absDelta)
            {
                // pass.
            }
            else
            {
                return;
            }

            // 同一フレームで事前に行われた計算のサイズよりdeltaが小さい場合、無視する。
            if (absDelta < Mathf.Abs(currentFrameMaxDeltaMovement.value))
            {
                return;
            }

            /* 
                ここでこのtouchがcertificateされた。

                この時点で、
                「もともとあるやつがそのまま動いた」
                「もともとあるやつが現在のやつよりも動いた」
                「新規に足された」
                の3択になる。

                これを分析する。
            */
            var continuedOrAwakeOrNewTouchId = eventData.pointerId;

            if (currentTouchIds.Contains(continuedOrAwakeOrNewTouchId))
            {
                // 旧知のタッチの中で、今までidentityが保持していたタッチが継続して動作した
                if (flickIdentity.TouchId == continuedOrAwakeOrNewTouchId)
                {
                    // 何もしない
                }
                else // 旧知のタッチの中で、休眠していたタッチが復帰して動作した
                {
                    // タッチが復帰してflickを開始したことを他のFlickableCornerに伝える
                    var approved = fNetworkObject.FlickAwakeRequest(this.CornerId, eventData.pointerId);

                    // 許可されなかったので終了
                    if (!approved)
                    {
                        return;
                    }

                    // 休眠から復帰したタッチの動きを扱う。
                    // 休眠していたタッチを再度動かすと、その初期位置は最初にそのタッチを開始した場所そのままになるため、そのままdiffを計算すると動作差分が一気に増えてしまってdragを継続するときに都合が悪い。
                    // そのため、新たに発生したタッチと動作が同じになるように、このイベントのpressPositionを[1フレーム前の値]に上書きする。
                    // 1フレーム前の値は、現在のpositionからdeltaを引いた位置になる。
                    // こうすることで、updateで実行されるUpdateFlickableViewMovement関数に対して改変したpressPositionが使われるようになり、滑らかに動くように
                    eventData.pressPosition = eventData.position - eventData.delta;
                }
            }
            else
            {
                // 新規に発生したタッチが動作したので、記録する。
                currentTouchIds.Add(continuedOrAwakeOrNewTouchId);
            }

            // 判定値を更新する。
            // updateで最終的な判定値の処理を行い、そのフレームでのdrag処理の実行を行う。
            currentFrameMaxDeltaMovement = new DeltaMovement(continuedOrAwakeOrNewTouchId, dirConstrainedDeltaMovementOfThisPointerDrag, eventData.pressPosition, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // タッチが離れたら入力中記録から消す。
            var endedTouchId = eventData.pointerId;
            if (currentTouchIds.Contains(endedTouchId))
            {
                currentTouchIds.Remove(endedTouchId);
            }

            // identityがない状態でのendは無視する
            if (flickIdentity == null)
            {
                return;
            }

            // 現在主として扱っているtouchId以外のendは無視する
            if (flickIdentity.TouchId != eventData.pointerId)
            {
                return;
            }

            // identityに登録されているtouchIdのポインターがendDragした場合、flickを終了する。

            // 実際にフリック可能な方向からフリック結果を取得する。
            var flickedDir = DetermineFlickResult(flickIdentity.flickableDir);

            // progressの更新を行う
            UpdateProgress(flickedDir);


            // flick成功か失敗かの判定を行う
            var isFlicked = flickedDir != FlickDirection.NONE;

            if (isFlicked)
            {
                // フリックが成立したのでアニメーションを開始する
                animationState = FlickAnimationState.PROCESSING;

                // ここでWillAppear/WillDisappearを流す
                NotifyAppearance(flickedDir);
            }
            else
            {
                // フリックが成立しなかったのでキャンセルアニメーションを開始する
                animationState = FlickAnimationState.CANCELLING;

                // TODO: 必要であればflickがキャンセルされたことを流す
            }

            // enumeratorを回してアニメーション中状態を処理する
            IEnumerator animationCor()
            {
                while (true)
                {
                    switch (animationState)
                    {
                        // flick不成立でのキャンセル中状態。
                        case FlickAnimationState.CANCELLING:
                            var cancellingCor = CancellingCor();

                            while (cancellingCor.MoveNext())
                            {
                                yield return null;
                            }

                            animationState = FlickAnimationState.NONE;
                            yield break;

                        // flick成立、目的位置への移動処理を行っている状態。
                        case FlickAnimationState.PROCESSING:
                            var processingCor = ProcessingCor(flickedDir);

                            while (processingCor.MoveNext())
                            {
                                yield return null;
                            }

                            animationState = FlickAnimationState.NONE;
                            yield break;

                        default:
                            // 何もしない
                            break;
                    }

                    yield return null;
                }
            }

            // animation用のCoroutineを作ってUpdateで回す。
            // こうすることで、不意に画面遷移が発生してもこのGOがなければ事故が発生しないようにする。
            // 画面遷移から戻ってくるときには初期化して欲しい気持ちがある。
            this.animationCor = animationCor();

            // TODO: identityの初期化をやっていいのかどうかがわからない。hiddenな上位でやらせた方がいい気はする。さて、それってどうしようか。
            flickIdentity = null;
        }

        private IEnumerator ProcessingCor(FlickDirection flickedDir)
        {
            var done = false;

            Action onDone = () =>
            {
                done = true;
            };

            var cancelled = false;
            Action onCancelled = () =>
            {
                cancelled = true;
            };

            var moveByUnitSizeVec = Vector2.zero;
            switch (flickedDir)
            {
                case FlickDirection.RIGHT:
                    moveByUnitSizeVec = new Vector2(MoveUnitSize, 0f);
                    break;
                case FlickDirection.LEFT:
                    moveByUnitSizeVec = new Vector2(-MoveUnitSize, 0f);
                    break;
                default:
                    Debug.LogError("unsupported dir:" + flickedDir);
                    break;
            }

            if (handler != null)
            {
                handler.OnFlickProcessAnimationRequired(this, initalPos + moveByUnitSizeVec, onDone, onCancelled);
            }
            else
            {
                onDone();
            }

            while (!cancelled && !done)
            {
                yield return null;
            }

            if (done)
            {
                // 位置をfixさせる
                SetFixedPositionFromInitialPos(moveByUnitSizeVec);

                // 通知を行う
                NotifyProcessed(flickedDir);
            }
            else if (cancelled)
            {
                var cancelCor = CancellingCor();
                while (cancelCor.MoveNext())
                {
                    yield return null;
                }
            }
        }

        private IEnumerator CancellingCor()
        {
            var done = false;
            Action onDone = () =>
            {
                done = true;
            };

            // キャンセルアニメーション開始
            if (handler != null)
            {
                handler.OnFlickCancelAnimationRequired(this, initalPos, onDone);
            }
            else
            {
                onDone();
            }

            while (!done)
            {
                yield return null;
            }

            // 位置をfixさせる
            ResetToInitialPosition();

            // キャンセル済み通知を行う
            NotifyCancelled();
        }

        // 関連するcornerの位置だけを、初期位置から差分だけ移動させる。
        public void UpdateRelatedCornerPositions()
        {
            var diff = currentRectTransform().anchoredPosition - initalPos;

            if (CornerFromLeft != null)
            {
                CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + diff;
            }
            if (CornerFromRight != null)
            {
                CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + diff;
            }
            if (CornerFromTop != null)
            {
                CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + diff;
            }
            if (CornerFromBottom != null)
            {
                CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + diff;
            }
        }

        private IEnumerator animationCor;

        private bool touchInScreenForEditor = false;
        private void Update()
        {
            // イベント処理の直後に呼び出されるUpdate

            // エディタでのみ、タッチインがどこから行われたかを判定するために、フレームごとにタッチが画面内にあるかどうかを取得する。
            {
#if UNITY_EDITOR
                var x = Input.mousePosition.x;
                var y = Input.mousePosition.y;
                if (0 <= x && x <= Screen.width && 0 <= y && y <= Screen.height)
                {
                    // in screen.
                    touchInScreenForEditor = true;
                }
                else// マウスがスクリーン外な場合、このフレームでのマウス位置をフレーム外に持っていく。
                {
                    touchInScreenForEditor = false;
                }
#endif
            }

            // アニメーション要素があれば動かす
            if (animationCor != null)
            {
                var cont = animationCor.MoveNext();
                if (!cont)
                {
                    animationCor = null;
                }
            }

            // 最低一つのdragがこのビュー上にあったことを検知し、動かす。
            // ここでこのビューが受け取る全てのマルチタッチのdragを収束させている。
            // TODO: 必要があればさらに上位で収束させる。ありそうなんだよな〜〜、、 と思ったがそうでもないっぽい、移動をうまくやってるから入力時はこのビューが拾ってくれる。なるほど？ 
            // いや単に全画面拾ってるっぽいが、、？ 
            // -> 気のせいだった、ちゃんとタッチ判定ない。はい。なんかしないといけない。少なくともhiddenな中央集権は一個必要だなあ。
            if (currentFrameMaxDeltaMovement.index != -2)
            {
                // アニメーション中ではない場合のみ、タッチ効果による移動を発生させる。
                if (animationState == FlickAnimationState.NONE)
                {
                    UpdateFlickableViewMovement(currentFrameMaxDeltaMovement.index, currentFrameMaxDeltaMovement.initialPos, currentFrameMaxDeltaMovement.currentPos);

                    // progressの更新を行う
                    var actualMoveDir = DetectFlickingDirection(currentFrameMaxDeltaMovement.currentPos - currentFrameMaxDeltaMovement.initialPos);
                    UpdateProgress(actualMoveDir);
                }

                // 次フレームのためにdeltaを初期化する
                currentFrameMaxDeltaMovement = new DeltaMovement(-2, 0f, Vector2.zero, Vector2.zero);
            }
        }

        // ビューのmoveを行う。
        private void UpdateFlickableViewMovement(int index, Vector2 initialPos, Vector2 currentPos)
        {
            // 従来のtouchのdragを検出した。
            if (flickIdentity.TouchId == index)
            {
                // 初期点からdir方向にdragした距離を計測する。
                var continuousDragDiffFromInitialOfThisDrag = GetConstraintedDiffMovement(flickIdentity.interactableDir, flickIdentity.flickableDir, initialPos, currentPos);

                // inherited + 特定の方向へと移動させたことにする。
                // 実際にflickできる方向を加味した移動サイズを出す。
                var continuousMoveSize = LimitMoveSizeByFlickableDir(flickIdentity.flickableDir, continuousDragDiffFromInitialOfThisDrag + flickIdentity.InheritedDiff);

                Move(continuousMoveSize, flickIdentity.interactableDir);

                // identityが持っているdiffの情報をアップデートする。
                flickIdentity.UpdateDiff(continuousDragDiffFromInitialOfThisDrag);
                return;
            }

            // 初期 or 追加されたtouchに加えて、新しいtouchによってdragが実行された。
            // 処理的には似通っているが、綺麗にするとめちゃくちゃ認識するのが難しくなるため、分けて書く。

            // touchIdが更新されることによって、「ここまでに移動していた距離」に対して差分値が発生することになる。
            // touchIdを変更することによってリレーが発生する。
            /*
                touch Aで動かす -> 初期位置からのdiffで位置計算をする
                touch Bに変更 + 動かす -> Bの初期位置からのdiff + Aで動いていた距離で、、
                のように、touchが足されるごとにdiffが+されていく。で、BのdiffはAを引いたものが適応される。
            */
            var newTouchId = index;

            // 新しいtouchのdragによる値を算出し、これまでのtouchで保持していた値をinheritする。
            flickIdentity.UpdateTouchId(newTouchId);

            // 初期点からdir方向にdragした距離を計測する。
            var newDragDiffFromInitialOfThisDrag = GetConstraintedDiffMovement(flickIdentity.interactableDir, flickIdentity.flickableDir, initialPos, currentPos);

            // inherited + 特定の方向へと移動させたことにする。
            // 実際にflickできる方向を加味した移動サイズを出す。
            var newMoveSize = LimitMoveSizeByFlickableDir(flickIdentity.flickableDir, newDragDiffFromInitialOfThisDrag + flickIdentity.InheritedDiff);

            Move(newMoveSize, flickIdentity.interactableDir);

            // identityが持っているdiffの情報をアップデートする。
            flickIdentity.UpdateDiff(newDragDiffFromInitialOfThisDrag);
        }

        public float ReactUnitSize;// 反応サイズ、これを超えたら要素をmoveUnitSizeまで移動させる。
        public float MoveUnitSize;// 反応後移動サイズ
        public float EmptyCornerUnitSize;// 虚無に対して引っ張ることができるサイズ


        // ここに要素がセットしてあればその方向に動作する
        public Corner CornerFromLeft;
        public Corner CornerFromRight;
        public Corner CornerFromTop;
        public Corner CornerFromBottom;

        private Vector2 cornerFromLeftInitialPos;
        private Vector2 cornerFromRightInitialPos;
        private Vector2 cornerFromTopInitialPos;
        private Vector2 cornerFromBottomInitialPos;


        // willDisappearを自身のコンテンツに出し、willAppearを関連する方向のコンテンツに出す
        private void NotifyAppearance(FlickDirection flickedDir)
        {
            // 上位のハンドラがあればそれに対して消える予定を通知する
            if (handler != null)
            {
                handler.FlickableCornerWillDisappear(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.CornerWillDisappear();
            }

            // これから表示される要素のcontentsにwillAppearを伝える
            switch (flickedDir)
            {
                case FlickDirection.RIGHT:
                    WillAppear(CornerFromLeft); break;
                case FlickDirection.LEFT:
                    WillAppear(CornerFromRight); break;
                case FlickDirection.UP:
                    WillAppear(CornerFromBottom); break;
                case FlickDirection.DOWN:
                    WillAppear(CornerFromTop); break;
            }
        }

        // willAppearを上下のコンテンツに送り出す
        private void WillAppear(Corner corner)
        {
            if (corner != null)
            {
                // 上流がある型であれば送り出す
                if (corner is FlickableCorner)
                {
                    ((FlickableCorner)corner).SendWillAppear();
                }

                // 下流にハンドラがいれば送り出す
                foreach (var containedUIComponent in corner.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerWillAppear();
                }
            }
        }

        // 上流へとWillAppearを送り出す
        private void SendWillAppear()
        {
            if (handler != null)
            {
                handler.FlickableCornerWillAppear(this);
            }
        }

        // キャンセル関連を流すFlickWillBack/Cancel
        private void NotifyCancelling(FlickDirection flickedDir)
        {
            // 上位のハンドラがあればそれに対して消える予定を通知する
            if (handler != null)
            {
                handler.FlickableCornerWillBack(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.CornerWillBack();
            }

            // これから表示される要素のcontentsにwillCancelを伝える
            switch (flickedDir)
            {
                case FlickDirection.RIGHT:
                    WillCancel(CornerFromLeft); break;
                case FlickDirection.LEFT:
                    WillCancel(CornerFromRight); break;
                case FlickDirection.UP:
                    WillCancel(CornerFromBottom); break;
                case FlickDirection.DOWN:
                    WillCancel(CornerFromTop); break;
            }
        }

        // willCancelを上下のコンテンツに送り出す
        private void WillCancel(Corner corner)
        {
            if (corner != null)
            {
                // 上流がある型であれば送り出す
                if (corner is FlickableCorner)
                {
                    ((FlickableCorner)corner).SendWillCancel();
                }

                // 下流にハンドラがいれば送り出す
                foreach (var containedUIComponent in corner.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerWillCancel();
                }
            }
        }

        // 上流へとWillCancelを送り出す
        private void SendWillCancel()
        {
            if (handler != null)
            {
                handler.FlickableCornerWillCancel(this);
            }
        }

        // disppearCancelledを自身のコンテンツに出し、appearCancelledを関連するコンテンツに出す
        private void NotifyCancelled()
        {
            // 上位のハンドラがあればそれに対して消える予定のキャンセルを通知する
            if (handler != null)
            {
                handler.FlickableCornerDisppearCancelled(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
            // TODO: GetComponentsInChildren<ICornerContent> あとでなんとかしよう。重そう。
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.CornerDisppearCancelled();
            }

            // 関連コンテンツが存在すれば引っ張られているはずなのでappearCancelを伝える
            AppearCancelled(CornerFromLeft);
            AppearCancelled(CornerFromRight);
            AppearCancelled(CornerFromBottom);
            AppearCancelled(CornerFromTop);
        }

        private void AppearCancelled(Corner corner)
        {
            if (corner != null)
            {
                // 上流があれば出現キャンセルを送り出す
                if (corner is FlickableCorner)
                {
                    ((FlickableCorner)corner).SendAppearCancelled();
                }

                // 下流に出現キャンセルを送り出す
                foreach (var containedUIComponent in corner.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerAppearCancelled();
                }
            }
        }

        // 上流へと出現キャンセルを送り出す
        private void SendAppearCancelled()
        {
            if (handler != null)
            {
                handler.FlickableCornerAppearCancelled(this);
            }
        }

        // didDisappearを自身のコンテンツに出し、didAppearを関連する方向のコンテンツに出す
        private void NotifyProcessed(FlickDirection resultDir)
        {
            // 上位のハンドラがあればそれに対して消えたことを通知する
            if (handler != null)
            {
                handler.FlickableCornerDidDisappear(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearを伝える
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.CornerDidDisappear();
            }

            // 関連コンテンツがあればappearを伝える
            switch (resultDir)
            {
                case FlickDirection.RIGHT:
                    DidAppear(CornerFromLeft); break;
                case FlickDirection.LEFT:
                    DidAppear(CornerFromRight); break;
                case FlickDirection.UP:
                    DidAppear(CornerFromBottom); break;
                case FlickDirection.DOWN:
                    DidAppear(CornerFromTop); break;
                default:
                    Debug.LogError("unhandled dir:" + resultDir);
                    break;
            }
        }

        private void DidAppear(Corner corner)
        {
            if (corner != null)
            {
                // 上流があれば出現完了を送り出す
                if (corner is FlickableCorner)
                {
                    ((FlickableCorner)corner).SendDidAppear();
                }

                // 下流に出現完了を送り出す
                foreach (var containedUIComponent in corner.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerDidAppear();
                }
            }
        }

        // 上流へと出現完了を送り出す
        private void SendDidAppear()
        {
            if (handler != null)
            {
                handler.FlickableCornerDidAppear(this);
            }
        }

        private FlickDirection DetectFlickingDirection(Vector2 delta)
        {
            if (0 < delta.x)
            {
                return FlickDirection.RIGHT;
            }
            else if (delta.x < 0)
            {
                return FlickDirection.LEFT;
            }
            else if (0 < delta.y)
            {
                return FlickDirection.UP;
            }
            else if (delta.y < 0)
            {
                return FlickDirection.DOWN;
            }

            return FlickDirection.NONE;
        }

        // インタラクション的に移動可能な方向を返す
        // 実際に移動先にcornerが存在するわけではなく、deltaに左右要素があり左右どちらかがフリック可能であれば左右を、上下どちらかが
        private FlickDirection GetInteractableDirection(Vector2 deltaMove)
        {
            // ここで、deltaMoveの値に応じて実際に利用できる方向の判定を行い、移動可能な方向を限定して返す。

            var dir = FlickDirection.NONE;

            // 左右フリックをカバーできる状態
            if (CornerFromLeft != null || CornerFromRight != null)
            {
                dir = dir | FlickDirection.RIGHT;
                dir = dir | FlickDirection.LEFT;

                // 上下左右がある場合でも、左右を優先
                return dir;
            }

            // 上下フリックをカバーできる状態
            if (CornerFromTop != null || CornerFromBottom != null)
            {
                dir = dir | FlickDirection.UP;
                dir = dir | FlickDirection.DOWN;

                return dir;
            }

            // TODO: ここから下に来ることはない

            // 右か左をカバー
            {
                var xSign = Mathf.Sign(deltaMove.x);

                // 右フリックをカバーできる状態
                if (CornerFromLeft != null)
                {
                    if (0f < xSign)
                    {
                        dir = dir | FlickDirection.RIGHT;
                        return dir;
                    }
                }
                // 左フリックをカバーできる状態
                if (CornerFromRight != null)
                {
                    if (xSign < 0f)
                    {
                        dir = dir | FlickDirection.LEFT;
                        return dir;
                    }
                }
            }

            // 上か下をカバー
            {
                var ySign = Mathf.Sign(deltaMove.y);

                // 下フリックをカバーできる状態
                if (CornerFromTop != null)
                {
                    if (0f < ySign)
                    {
                        dir = dir | FlickDirection.DOWN;
                        return dir;
                    }
                }
                // 上フリックをカバーできる状態
                if (CornerFromBottom != null)
                {
                    if (ySign < 0f)
                    {
                        dir = dir | FlickDirection.UP;
                        return dir;
                    }
                }
            }

            return dir;
        }


        // 実際にフリック処理が発生させられる方向を返す。
        private FlickDirection GetFlickableDirection(FlickDirection interactableDir)
        {
            var dir = FlickDirection.NONE;

            // 左ドラッグが可能な場合左右にドラッグできる状態のため、左右から実際にフリック可能な方向をチョイスする。
            if (interactableDir.HasFlag(FlickDirection.LEFT))
            {
                // 左右にインタラクション可能だが、実際に発生させられるフリック処理は右、左、左右のどれか一つ。
                if (CornerFromRight != null)
                {
                    dir = dir | FlickDirection.LEFT;
                }
                if (CornerFromLeft != null)
                {
                    dir = dir | FlickDirection.RIGHT;
                }
                return dir;
            }

            // 上ドラッグが可能な場合上下にドラッグできる状態のため、上下から実際にフリック可能な方向をチョイスする。
            if (interactableDir.HasFlag(FlickDirection.UP))
            {
                // 上下にインタラクション可能だが、実際に発生させられるフリック処理は上、下、上下のどれか一つ。
                if (CornerFromTop != null)
                {
                    dir = dir | FlickDirection.DOWN;
                }
                if (CornerFromBottom != null)
                {
                    dir = dir | FlickDirection.UP;
                }
                return dir;
            }

            return dir;
        }

        // 初期フリック方向を見積もる
        // 右 -> 左 -> 下 -> 上 の順に判定している。
        private FlickDirection EstimateFlickDir(Vector2 delta)
        {
            if (0 < delta.x)// 右に行こうとしている = 右フリック
            {
                return FlickDirection.RIGHT;
            }
            else if (delta.x < 0)
            {
                return FlickDirection.LEFT;
            }
            else if (delta.y < 0)
            {
                return FlickDirection.DOWN;
            }
            else if (0 < delta.y)
            {
                return FlickDirection.UP;
            }

            return FlickDirection.NONE;
        }

        // deltaから、現在設定されているdirに該当するmovement値を出力する。flickしない方向の値を消去する。
        private float GetConstraintedDeltaMovement(FlickDirection dir, Vector2 delta)
        {
            var x = delta.x;
            var y = delta.y;

            // 左右
            if (dir.HasFlag(FlickDirection.RIGHT) || dir.HasFlag(FlickDirection.LEFT))
            {
                // 左右に動く
            }
            else
            {
                // 左右には動かない
                x = 0;
            }

            // 上下
            if (dir.HasFlag(FlickDirection.DOWN) || dir.HasFlag(FlickDirection.UP))
            {
                // 上下に動く
            }
            else
            {
                // 上下には動かない
                y = 0;
            }

            // 上下に動かない場合は左右に動くはずなのでxを返す
            if (y == 0)
            {
                return x;
            }

            // 上下に動く場合はyを返す

            return y;
        }

        // インタラクション可能な方向、フリック可能な方向から、実際に移動に使用する値を出力する。
        private Vector2 GetConstraintedDiffMovement(FlickDirection interactableDir, FlickDirection flickableDir, Vector2 basePos, Vector2 currentPos)
        {
            // 差分
            var baseDiff = currentPos - basePos;

            var x = baseDiff.x;
            var y = baseDiff.y;

            // 左右にインタラクション可能
            if (interactableDir.HasFlag(FlickDirection.RIGHT) || interactableDir.HasFlag(FlickDirection.LEFT))
            {
                // pass.
            }
            else
            {
                // 左右には動かない
                x = 0;
            }

            // 上下にインタラクション可能
            if (interactableDir.HasFlag(FlickDirection.DOWN) || interactableDir.HasFlag(FlickDirection.UP))
            {
                // pass.
            }
            else
            {
                // 上下には動かない
                y = 0;
            }

            return new Vector2(x, y);
        }

        // flickableDirを元に移動サイズを制限する。
        // flickできない方向へは、指定したサイズ以上は移動できない。
        private Vector2 LimitMoveSizeByFlickableDir(FlickDirection flickableDir, Vector2 moveSize)
        {
            var x = moveSize.x;
            var y = moveSize.y;

            // 実際には右フリックが不可なので、EmptyCornerUnitSizeまでの引っ張りが可能
            if (!flickableDir.HasFlag(FlickDirection.RIGHT))
            {
                x = Mathf.Min(EmptyCornerUnitSize, x);
            }
            // 実際には左フリックが不可なので、-EmptyCornerUnitSizeまでの引っ張りが可能
            if (!flickableDir.HasFlag(FlickDirection.LEFT))
            {
                x = Mathf.Max(-EmptyCornerUnitSize, x);
            }

            // TODO: この辺試してない
            // 実際には上フリックが不可なので、EmptyCornerUnitSizeまでの引っ張りが可能
            if (!flickableDir.HasFlag(FlickDirection.UP))
            {
                y = Mathf.Min(EmptyCornerUnitSize, y);
            }

            // TODO: この辺試してない
            // 実際には下フリックが不可なので、-EmptyCornerUnitSizeまでの引っ張りが可能
            if (!flickableDir.HasFlag(FlickDirection.DOWN))
            {
                y = Mathf.Max(-EmptyCornerUnitSize, y);
            }

            return new Vector2(x, y);
        }


        // 自身と各コーナーの初期位置を更新する
        private void UpdateInitialPos()
        {
            initalPos = currentRectTransform().anchoredPosition;

            if (CornerFromLeft != null)
            {
                cornerFromLeftInitialPos = CornerFromLeft.currentRectTransform().anchoredPosition;
            }
            if (CornerFromRight != null)
            {
                cornerFromRightInitialPos = CornerFromRight.currentRectTransform().anchoredPosition;
            }
            if (CornerFromTop != null)
            {
                cornerFromTopInitialPos = CornerFromTop.currentRectTransform().anchoredPosition;
            }
            if (CornerFromBottom != null)
            {
                cornerFromBottomInitialPos = CornerFromBottom.currentRectTransform().anchoredPosition;
            }
        }

        // 自身と各コーナーの位置を初期位置に戻す
        private void ResetToInitialPosition()
        {
            currentRectTransform().anchoredPosition = initalPos;

            if (CornerFromLeft != null)
            {
                CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos;
            }
            if (CornerFromRight != null)
            {
                CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos;
            }
            if (CornerFromTop != null)
            {
                CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos;
            }
            if (CornerFromBottom != null)
            {
                CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos;
            }
        }

        // 初期位置から換算した差分を与えて、このCornerと関連するCornerの位置を確定させる。
        private void SetFixedPositionFromInitialPos(Vector2 diff)
        {
            // 自身の位置を確定させる
            currentRectTransform().anchoredPosition = initalPos + diff;

            // 関連cornerの位置を確定させる + 関連コーナーにさらに関連がある場合連動させる。
            // 自身を渡しているのは、関連する要素が保持しているfrom系のcornerがこれ自身な可能性があり、そうすると二重に移動されてしまうのを、自身と対象を比較させて不要な移動だけをキャンセルするため。
            if (CornerFromLeft != null)
            {
                CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + diff;
                if (CornerFromLeft is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromLeft).UpdateRelatedCornerPositions(this, diff);
                }
            }
            if (CornerFromRight != null)
            {
                CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + diff;
                if (CornerFromRight is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromRight).UpdateRelatedCornerPositions(this, diff);
                }
            }
            if (CornerFromTop != null)
            {
                CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + diff;
                if (CornerFromTop is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromTop).UpdateRelatedCornerPositions(this, diff);
                }
            }
            if (CornerFromBottom != null)
            {
                CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + diff;
                if (CornerFromBottom is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromBottom).UpdateRelatedCornerPositions(this, diff);
                }
            }
        }

        private void Move(Vector2 moveDiff, FlickDirection inteactableDir)
        {
            currentRectTransform().anchoredPosition = initalPos + moveDiff;

            // 作用を受けるcornerの位置も、上記のdiffをもとに動作させる
            switch (inteactableDir)
            {
                case FlickDirection.RIGHT | FlickDirection.LEFT:
                    if (CornerFromLeft != null)
                    {
                        CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + moveDiff;
                    }
                    if (CornerFromRight != null)
                    {
                        CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + moveDiff;
                    }
                    break;
                case FlickDirection.RIGHT:
                    CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + moveDiff;
                    break;
                case FlickDirection.LEFT:
                    CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + moveDiff;
                    break;
                case FlickDirection.UP | FlickDirection.DOWN:
                    if (CornerFromBottom != null)
                    {
                        CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + moveDiff;
                    }
                    if (CornerFromTop != null)
                    {
                        CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + moveDiff;
                    }
                    break;
                case FlickDirection.UP:
                    CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + moveDiff;
                    break;
                case FlickDirection.DOWN:
                    CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + moveDiff;
                    break;
                default:
                    Debug.LogError("unsupported flickDir:" + inteactableDir);
                    break;
            }
        }

        private void UpdateProgress(FlickDirection currentMovedDirection)
        {
            var xDist = initalPos.x - currentRectTransform().anchoredPosition.x;
            var yDist = initalPos.y - currentRectTransform().anchoredPosition.y;

            // 横方向
            if (xDist != 0)
            {
                var progress = Mathf.Abs(xDist) / ReactUnitSize;

                var appearProgress = Mathf.Min(1f, progress);
                var disappearProgress = Mathf.Max(0f, 1.0f - progress);

                // 上流があればdisappear度合いを伝える
                if (handler != null)
                {
                    handler.FlickableCornerDisppearProgress(this, disappearProgress);
                }

                // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
                foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerDisppearProgress(disappearProgress);
                }

                // これから表示される要素のcontentsにappearProgressを伝える
                switch (currentMovedDirection)
                {
                    case FlickDirection.RIGHT:
                        AppearProgress(CornerFromLeft, appearProgress);
                        break;
                    case FlickDirection.LEFT:
                        AppearProgress(CornerFromRight, appearProgress);
                        break;
                }

                return;
            }

            // 縦方向
            if (yDist != 0)
            {
                var progress = Mathf.Abs(yDist) / ReactUnitSize;

                var appearProgress = Mathf.Min(1f, progress);
                var disappearProgress = Mathf.Max(0f, 1.0f - progress);

                // 上流があればdisappear度合いを伝える
                if (handler != null)
                {
                    handler.FlickableCornerDisppearProgress(this, disappearProgress);
                }

                // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
                foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerDisppearProgress(disappearProgress);
                }

                // これから表示される要素のcontentsにappearProgressを伝える
                switch (currentMovedDirection)
                {
                    case FlickDirection.UP:
                        AppearProgress(CornerFromBottom, appearProgress);
                        break;
                    case FlickDirection.DOWN:
                        AppearProgress(CornerFromTop, appearProgress);
                        break;
                }
            }
        }

        // 出現度合いを各cornerに送り出す
        private void AppearProgress(Corner corner, float progress)
        {
            if (corner != null)
            {
                // 上流があれば進捗を伝える
                if (corner is FlickableCorner)
                {
                    ((FlickableCorner)corner).SendAppearProgress(progress);
                }

                // 下流に進捗を伝える
                foreach (var containedUIComponent in corner.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.CornerAppearProgress(progress);
                }
            }
        }

        // 出現度合いを上流に通知する
        private void SendAppearProgress(float progress)
        {
            if (handler != null)
            {
                handler.FlickableCornerAppearProgress(this, progress);
            }
        }

        // フリック成立かどうかを成立した方向で返す。
        // 成立していない場合NONEが帰ってくる。
        private FlickDirection DetermineFlickResult(FlickDirection flickDir)
        {
            var xDist = initalPos.x - currentRectTransform().anchoredPosition.x;
            var yDist = initalPos.y - currentRectTransform().anchoredPosition.y;

            if (flickDir.HasFlag(FlickDirection.RIGHT) && ReactUnitSize <= -xDist)
            {
                return FlickDirection.RIGHT;
            }

            if (flickDir.HasFlag(FlickDirection.LEFT) && ReactUnitSize <= xDist)
            {
                return FlickDirection.LEFT;
            }

            if (flickDir.HasFlag(FlickDirection.UP) && ReactUnitSize <= -yDist)
            {
                return FlickDirection.UP;
            }

            if (flickDir.HasFlag(FlickDirection.DOWN) && ReactUnitSize <= yDist)
            {
                return FlickDirection.DOWN;
            }

            // flick判定の結果、該当なし
            return FlickDirection.NONE;
        }

        // 関連するCornerの位置を更新する
        // 再帰的に関連するcornerの関連、、までを移動させる。
        private void UpdateRelatedCornerPositions(FlickableCorner flickedSource, Vector2 movedVector)
        {
            // 関連するCornerの要素も連鎖させて移動させる。呼び出し元のオブジェクトと同じオブジェクトはすでに移動済なので移動させない。
            if (CornerFromRight != null && CornerFromRight != flickedSource)
            {
                CornerFromRight.currentRectTransform().anchoredPosition = CornerFromRight.currentRectTransform().anchoredPosition + movedVector;
                if (CornerFromRight is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromRight).UpdateRelatedCornerPositions(this, movedVector);
                }
            }

            if (CornerFromLeft != null && CornerFromLeft != flickedSource)
            {
                CornerFromLeft.currentRectTransform().anchoredPosition = CornerFromLeft.currentRectTransform().anchoredPosition + movedVector;
                if (CornerFromLeft is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromLeft).UpdateRelatedCornerPositions(this, movedVector);
                }
            }

            if (CornerFromTop != null && CornerFromTop != flickedSource)
            {
                CornerFromTop.currentRectTransform().anchoredPosition = CornerFromTop.currentRectTransform().anchoredPosition + movedVector;
                if (CornerFromTop is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromTop).UpdateRelatedCornerPositions(this, movedVector);
                }
            }

            if (CornerFromBottom != null && CornerFromBottom != flickedSource)
            {
                CornerFromBottom.currentRectTransform().anchoredPosition = CornerFromBottom.currentRectTransform().anchoredPosition + movedVector;
                if (CornerFromBottom is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromBottom).UpdateRelatedCornerPositions(this, movedVector);
                }
            }
        }


        // flick情報を統合的に扱うための共有インスタンス
        private FlickableCornersNetwork fNetworkObject;
        private void UpdateNetwork(ref FlickableCornersNetwork networkRef)
        {
            this.fNetworkObject = networkRef;
        }


        // 接続されている全てのflickableCornerの集合を更新する
        // どのFlickableCornerが起点となって実行されても問題ない。
        // 初期化時、更新時に実行する。
        private void UpdateNetworkByAddedOrRemovedRelatedCorner(ref FlickableCornersNetwork sharedRef)
        {
            UpdateNetworkRecursively(this, ref sharedRef);
        }

        private void UpdateNetworkRecursively(FlickableCorner root, ref FlickableCornersNetwork networkRef)
        {
            networkRef.Join(this);

            if (CornerFromRight != null && CornerFromRight != root && CornerFromRight is FlickableCorner)
            {
                var fCorner = (FlickableCorner)CornerFromRight;
                networkRef.Join(fCorner);
                fCorner.UpdateNetwork(ref networkRef);
                fCorner.UpdateNetworkRecursively(this, ref networkRef);
            }

            if (CornerFromLeft != null && CornerFromLeft != root && CornerFromLeft is FlickableCorner)
            {
                var fCorner = (FlickableCorner)CornerFromLeft;
                networkRef.Join(fCorner);
                fCorner.UpdateNetwork(ref networkRef);
                fCorner.UpdateNetworkRecursively(this, ref networkRef);
            }

            if (CornerFromTop != null && CornerFromTop != root && CornerFromTop is FlickableCorner)
            {
                var fCorner = (FlickableCorner)CornerFromTop;
                networkRef.Join(fCorner);
                fCorner.UpdateNetwork(ref networkRef);
                fCorner.UpdateNetworkRecursively(this, ref networkRef);
            }

            if (CornerFromBottom != null && CornerFromBottom != root && CornerFromBottom is FlickableCorner)
            {
                var fCorner = (FlickableCorner)CornerFromBottom;
                networkRef.Join(fCorner);
                fCorner.UpdateNetwork(ref networkRef);
                fCorner.UpdateNetworkRecursively(this, ref networkRef);
            }
        }

        // 接続しているCornerすべてのRectTransformを取得する
        public RectTransform[] CollectRelatedFlickableCornersRectTrans()
        {
            var rectTrans = new List<RectTransform>();
            CollectRelatedRectTrans(ref rectTrans, this);
            return rectTrans.ToArray();
        }

        private void CollectRelatedRectTrans(ref List<RectTransform> rectTrans, FlickableCorner root)
        {
            rectTrans.Add(this.currentRectTransform());

            if (CornerFromRight != null && CornerFromRight != root)
            {
                rectTrans.Add(CornerFromRight.currentRectTransform());
                if (CornerFromRight is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromRight).CollectRelatedRectTrans(ref rectTrans, this);
                }
            }

            if (CornerFromLeft != null && CornerFromLeft != root)
            {
                rectTrans.Add(CornerFromLeft.currentRectTransform());
                if (CornerFromLeft is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromLeft).CollectRelatedRectTrans(ref rectTrans, this);
                }
            }

            if (CornerFromTop != null && CornerFromTop != root)
            {
                rectTrans.Add(CornerFromTop.currentRectTransform());
                if (CornerFromTop is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromTop).CollectRelatedRectTrans(ref rectTrans, this);
                }
            }

            if (CornerFromBottom != null && CornerFromBottom != root)
            {
                rectTrans.Add(CornerFromBottom.currentRectTransform());
                if (CornerFromBottom is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromBottom).CollectRelatedRectTrans(ref rectTrans, this);
                }
            }
        }


        // fromからtoへとつながる経路探索を行う
        public static bool TryFindingAutoFlickRoute(FlickableCorner from, FlickableCorner to, out GamenDriver driver)
        {
            driver = null;

            if (from == null)
            {
                return false;
            }
            if (to == null)
            {
                return false;
            }

            // 接続されていれば、経路が見つかってステップが返せるはず。
            if (IsConnected(from, to, out var direction, out var steps))
            {
                driver = new GamenDriver(steps);
                return true;
            }

            return false;
        }

        // FlickableCorner同士の上下左右の接続を見て、接続があればtrueを返す。
        private static bool IsConnected(FlickableCorner from, FlickableCorner to, out FlickDirection direction, out Corner[] route)
        {
            // 同一の親の下にいない場合、接続されていない。
            if (from.transform.parent != to.transform.parent)
            {
                direction = FlickDirection.NONE;
                route = null;
                return false;
            }

            // TODO: ななめは勘弁してほしい(なんかロジックあるだろうけど正直そんなキモいUIをサポートしたくない、、、)
            // fromからtoへと、接続があるかどうか探す。
            if (SearchStart(FlickDirection.LEFT, from, to, out var stepsToRight))
            {
                direction = FlickDirection.LEFT;
                route = stepsToRight.ToArray();
                return true;
            }
            if (SearchStart(FlickDirection.RIGHT, from, to, out var stepsToLeft))
            {
                direction = FlickDirection.RIGHT;
                route = stepsToLeft.ToArray();
                return true;
            }
            if (SearchStart(FlickDirection.UP, from, to, out var stepsToDown))
            {
                direction = FlickDirection.UP;
                route = stepsToDown.ToArray();
                return true;
            }
            if (SearchStart(FlickDirection.DOWN, from, to, out var stepsToUp))
            {
                direction = FlickDirection.DOWN;
                route = stepsToUp.ToArray();
                return true;
            }

            direction = FlickDirection.NONE;
            route = null;
            return false;
        }

        private static bool SearchStart(FlickDirection dir, FlickableCorner current, FlickableCorner target, out List<Corner> steps)
        {
            steps = new List<Corner>();

            // 起点の情報を追加
            steps.Add(current);
            if (Search(dir, current, target, ref steps))
            {
                // 終点の情報を追加
                steps.Add(target);
                return true;
            }

            return false;
        }

        private static bool Search(FlickDirection dir, FlickableCorner current, FlickableCorner target, ref List<Corner> steps)
        {
            Corner directedCorner = null;
            switch (dir)
            {
                case FlickDirection.LEFT:
                    directedCorner = current.CornerFromRight;
                    break;
                case FlickDirection.RIGHT:
                    directedCorner = current.CornerFromLeft;
                    break;
                case FlickDirection.UP:
                    directedCorner = current.CornerFromBottom;
                    break;
                case FlickDirection.DOWN:
                    directedCorner = current.CornerFromTop;
                    break;
            }

            if (directedCorner != null)
            {
                // その方向にあったのがターゲットだった
                if (directedCorner == target)
                {
                    return true;
                }

                if (directedCorner is FlickableCorner)
                {
                    // 違ったが、続きがある可能性があるため、ここまでのステップを記録する。
                    steps.Add(directedCorner);
                    return Search(dir, (FlickableCorner)directedCorner, target, ref steps);
                }
            }

            steps = null;
            return false;
        }

        private void FrameLog(string message)
        {
            Debug.Log("message:" + message + " frame:" + Time.frameCount + "\tidentity:" + flickIdentity);
        }
    }
}