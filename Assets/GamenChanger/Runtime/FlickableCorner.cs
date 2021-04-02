using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    /*
        フリック可能なCorner
        // TODO: 対象となるhandlerやcornerを見つけてinspectorに表示不可だがクリックすると光るボタンとして表示したい感じある。
    */
    public class FlickableCorner : Corner, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IBeginDragHandler, IPointerEnterHandler
    {
        private enum FlickState
        {
            NONE,
            INIT,
            BEGIN,
            FLICKING,
            PROCESSING,
            CANCELLING,
        }

        private FlickState state = FlickState.NONE;
        private FlickDirection flickDir;
        private Vector2 initalPos;

        private IFlickableCornerHandler parentHandler;

        public new void Awake()
        {
            base.SetSubclassReloadAction(
                () =>
                {
                    // 親を探して、IFlickableCornerFocusHandlerを持っているオブジェクトがあったら、変更があるたびに上流に通知を流す。
                    if (transform.parent != null)
                    {
                        parentHandler = transform.parent.GetComponent<IFlickableCornerHandler>();
                    }
                }
            );

            base.Awake();
        }

        // 開始条件を判定するイベント
        public void OnPointerEnter(PointerEventData eventData)
        {
            switch (state)
            {
                case FlickState.NONE:
                    break;
                default:
                    return;
            }

            if (eventData.eligibleForClick && eventData.pointerEnter == this.gameObject && !touchInScreen)
            {
                // 実機でも動作する、このオブジェクトをdrag中にできる方法。
                eventData.pointerDrag = this.gameObject;
                OnInitializePotentialDrag(eventData);
                return;
            }
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            switch (state)
            {
                case FlickState.NONE:
                    // pass.
                    break;
                // 最終アニメーション動作中なので入力を無視する
                case FlickState.PROCESSING:
                case FlickState.CANCELLING:
                    return;
                case FlickState.INIT:
                    // 含まれるボタンを押したりすると発生する。無視していいやつ。
                    break;
                default:
                    Debug.LogError("OnInitializePotentialDrag unhandled state:" + state);
                    state = FlickState.NONE;
                    return;
            }

            if (eventData.pointerDrag != this.gameObject)
            {
                // このオブジェクトではないものの上で発生したので、無効にする。
                state = FlickState.NONE;
                return;
            }

            // 開始
            state = FlickState.INIT;
            NotifyTouch();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.pointerDrag != this.gameObject)
            {
                // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
                state = FlickState.NONE;
                return;
            }

            switch (state)
            {
                case FlickState.INIT:
                    // pass.
                    break;
                case FlickState.PROCESSING:
                case FlickState.CANCELLING:
                    // すでにend中なので無視する
                    return;
                default:
                    // イレギュラーなので解除
                    state = FlickState.NONE;
                    return;
            }

            // 最初のframeのflick操作を行う

            var delta = eventData.delta;

            // フリック方向を確定させる(左右 or 上下)
            var availableFlickDir = GetAvailableDirection(delta);

            // 利用できる方向がない場合、無効化する。
            if (availableFlickDir == FlickDirection.NONE)
            {
                state = FlickState.NONE;
                return;
            }

            flickDir = availableFlickDir;

            // from系の初期位置を保持
            UpdateInitialPos();

            // eventDataのパラメータを上書きし、指定のオブジェクトをドラッグしている状態に拘束する
            var moveDiff = ApplyConstraintToDir(flickDir, eventData);

            // 動かす
            Move(moveDiff);

            // 保持しているcornerと関連するcornerに対して、willDisappearとwillAppearを通知する。
            NotifyAppearance(eventData.delta);
            // TODO: この時に向かった方向とは逆の方向にdragし、なおかつ初期値を超えたタイミングで、キャンセルを流してなおかつ反対側にあるコンテンツがあればwillAppearを呼び出したい。

            state = FlickState.BEGIN;
        }

        // 継続条件チェックを行うイベント
        public void OnDrag(PointerEventData eventData)
        {
            // 特定状態以外無視する
            switch (state)
            {
                case FlickState.BEGIN:
                    state = FlickState.FLICKING;
                    // pass.
                    break;
                case FlickState.FLICKING:
                    // pass.
                    break;
                case FlickState.NONE:
                    // 無視する
                    return;
                case FlickState.PROCESSING:
                case FlickState.CANCELLING:
                    // すでにend中なので無視する
                    return;
                default:
                    // Debug.LogError("unhandled state:" + state);
                    state = FlickState.NONE;
                    return;
            }

            // drag対象がついて行ってない状態なので、終了させるという条件が必要になる。
            // 画面外にdragしたら終了
            if (0 < eventData.position.x && eventData.position.x < Screen.width && 0 < eventData.position.y && eventData.position.y < Screen.height)
            {
                // pass.
            }
            else
            {
                // 画面外にタッチが飛び出したので、drag終了する。
                OnEndDrag(eventData);
                return;
            }

            // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
            if (eventData.pointerDrag != this.gameObject)
            {
                // TODO: これって発生するのかな、、
                Debug.Log("drag ハズレ3");
            }

            // eventDataのパラメータを上書きし、指定のオブジェクトをドラッグしている状態に拘束する
            var moveDiff = ApplyConstraintToDir(flickDir, eventData);

            // ドラッグ継続
            Move(moveDiff);

            var actualMoveDir = DetectFlickingDirection(eventData.delta);

            //progressの更新を行う
            UpdateProgress(actualMoveDir);
        }

        // 終了時イベント
        public void OnEndDrag(PointerEventData eventData)
        {
            // 特定状態以外無視する
            switch (state)
            {
                case FlickState.FLICKING:
                    // pass.
                    break;
                case FlickState.NONE:
                    // 無視する
                    return;
                case FlickState.PROCESSING:
                case FlickState.CANCELLING:
                    // すでにend中なので無視する
                    return;
                default:
                    Debug.LogError("unhandled state:" + state);
                    state = FlickState.NONE;
                    return;
            }

            // 正常にflickの終了に到達したので、flick発生かどうかを判定する。

            // flickが発生したかどうかチェックし、発生していれば移動を完了させる処理モードに入る。
            // そうでなければ下の位置に戻すキャンセルモードに入る。
            var flickedDir = DetermineFlickResult();

            // progressの更新を行う
            UpdateProgress(flickedDir);

            var isFlicked = flickedDir != FlickDirection.NONE;
            if (isFlicked)
            {
                state = FlickState.PROCESSING;
            }
            else
            {
                state = FlickState.CANCELLING;
            }

            // enumeratorを回してアニメーション中状態を処理する
            IEnumerator animationCor()
            {
                while (true)
                {
                    switch (state)
                    {
                        // flick不成立でのキャンセル中状態。
                        case FlickState.CANCELLING:
                            var cancellingCor = CancellingCor();

                            while (cancellingCor.MoveNext())
                            {
                                yield return null;
                            }

                            state = FlickState.NONE;
                            yield break;

                        // flick成立、目的位置への移動処理を行っている状態。
                        case FlickState.PROCESSING:
                            var processingCor = ProcessingCor(flickedDir);

                            while (processingCor.MoveNext())
                            {
                                yield return null;
                            }

                            state = FlickState.NONE;
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
            this.animationCor = animationCor();
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

            parentHandler.OnProcessAnimationRequired(this, initalPos + moveByUnitSizeVec, onDone, onCancelled);

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
            parentHandler.OnCancelAnimationRequired(this, initalPos, onDone);

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

        private bool touchInScreen = false;
        private void Update()
        {
            // タッチインがどこから行われたかを判定するために、フレームごとにタッチが画面内にあるかどうかを取得する。
#if UNITY_EDITOR
            var x = Input.mousePosition.x;
            var y = Input.mousePosition.y;
            if (0 <= x && x <= Screen.width && 0 <= y && y <= Screen.height)
            {
                // in screen.
                touchInScreen = true;
            }
            else// マウスがスクリーン外な場合、このフレームでのマウス位置をフレーム外に持っていく。
            {
                touchInScreen = false;
            }
#else
            // タッチがスクリーン内にない場合、このフレームでのタッチ可否をfalseにする。
            if (Input.touchCount == 0)
            {
                touchInScreen = false;
            }
            else
            {
                touchInScreen = true;
            }
#endif

            // アニメーション要素があれば動かす
            if (animationCor != null)
            {
                var cont = animationCor.MoveNext();
                if (!cont)
                {
                    animationCor = null;
                }
            }
        }

        public float ReactUnitSize;// 反応サイズ、これを超えたら要素をmoveUnitSizeまで移動させる。
        public float MoveUnitSize;// 反応後移動サイズ


        // ここに要素がセットしてあればその方向に動作する
        public Corner CornerFromLeft;
        public Corner CornerFromRight;
        public Corner CornerFromTop;
        public Corner CornerFromBottom;

        private Vector2 cornerFromLeftInitialPos;
        private Vector2 cornerFromRightInitialPos;
        private Vector2 cornerFromTopInitialPos;
        private Vector2 cornerFromBottomInitialPos;


        private void NotifyTouch()
        {
            // 上位のハンドラがあればそれに対してタッチ反応があったことを通知する
            if (parentHandler != null)
            {
                parentHandler.Touch(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
            // TODO: GetComponentsInChildren系を消したいところ。
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.Touch();
            }
        }

        // willDisappearを自身のコンテンツに出し、willAppearを関連する方向のコンテンツに出す
        private void NotifyAppearance(Vector2 delta)
        {
            // 上位のハンドラがあればそれに対して消える予定を通知する
            if (parentHandler != null)
            {
                parentHandler.WillDisappear(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.WillDisappear();
            }

            var actualMoveDir = DetectFlickingDirection(delta);

            // これから表示される要素のcontentsにwillAppearを伝える
            switch (actualMoveDir)
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
                    containedUIComponent.WillAppear();
                }
            }
        }

        // 上流へとWillAppearを送り出す
        private void SendWillAppear()
        {
            if (parentHandler != null)
            {
                parentHandler.WillAppear(this);
            }
        }

        // disppearCancelledを自身のコンテンツに出し、appearCancelledを関連するコンテンツに出す
        private void NotifyCancelled()
        {
            // 上位のハンドラがあればそれに対して消える予定のキャンセルを通知する
            if (parentHandler != null)
            {
                parentHandler.DisppearCancelled(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
            // TODO: GetComponentsInChildren<ICornerContent> あとでなんとかしよう。重そう。
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.DisppearCancelled();
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
                    containedUIComponent.AppearCancelled();
                }
            }
        }

        // 上流へと出現キャンセルを送り出す
        private void SendAppearCancelled()
        {
            if (parentHandler != null)
            {
                parentHandler.AppearCancelled(this);
            }
        }

        // didDisappearを自身のコンテンツに出し、didAppearを関連する方向のコンテンツに出す
        private void NotifyProcessed(FlickDirection resultDir)
        {
            // 上位のハンドラがあればそれに対して消えたことを通知する
            if (parentHandler != null)
            {
                parentHandler.DidDisappear(this);
            }

            // 自身の持っているcontentsに対応のものがあればdisappearを伝える
            foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
            {
                containedUIComponent.DidDisappear();
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
                    containedUIComponent.DidAppear();
                }
            }
        }

        // 上流へと出現完了を送り出す
        private void SendDidAppear()
        {
            if (parentHandler != null)
            {
                parentHandler.DidAppear(this);
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

        // 移動可能な方向を返す
        private FlickDirection GetAvailableDirection(Vector2 deltaMove)
        {
            var estimatedFlickDir = EstimateFlickDir(deltaMove);

            // 上位のハンドラがあれば追加する方向のリクエストを行う
            if (parentHandler != null)
            {
                parentHandler.OnFlickRequestFromFlickableCorner(this, ref CornerFromLeft, ref CornerFromRight, ref CornerFromTop, ref CornerFromBottom, estimatedFlickDir);
            }

            // ここで、deltaMoveの値に応じて実際に利用できる方向の判定を行い、移動可能な方向を限定して返す。

            var dir = FlickDirection.NONE;

            // 左右フリックをカバーできる状態
            if (CornerFromLeft != null && CornerFromRight != null)
            {
                dir = dir | FlickDirection.RIGHT;
                dir = dir | FlickDirection.LEFT;

                // 上下左右がある場合でも、左右を優先
                return dir;
            }

            // 上下フリックをカバーできる状態
            if (CornerFromTop != null && CornerFromBottom != null)
            {
                dir = dir | FlickDirection.UP;
                dir = dir | FlickDirection.DOWN;

                return dir;
            }

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


        // dirを元に、移動できる方向を制限する。
        private Vector2 ApplyConstraintToDir(FlickDirection dir, PointerEventData eventData)
        {
            var basePos = eventData.pressPosition;// 押したworld位置
            var currentPos = eventData.position;// 現在のタッチのworld位置

            // 差分
            var baseDiff = currentPos - basePos;

            var x = baseDiff.x;
            var y = baseDiff.y;

            // 左右
            if (dir.HasFlag(FlickDirection.RIGHT) || dir.HasFlag(FlickDirection.LEFT))
            {
                if (!dir.HasFlag(FlickDirection.RIGHT))
                {
                    // 右フリックはできない前提
                    x = Mathf.Min(0, x);
                }
                if (!dir.HasFlag(FlickDirection.LEFT))
                {
                    // 左フリックはできない前提
                    x = Mathf.Max(0, x);
                }
            }
            else
            {
                // 左右には動かない
                x = 0;
            }

            // 上下
            if (dir.HasFlag(FlickDirection.DOWN) || dir.HasFlag(FlickDirection.UP))
            {
                if (!dir.HasFlag(FlickDirection.DOWN))
                {
                    // 下フリックはできない前提
                    y = Mathf.Max(0, y);
                }
                if (!dir.HasFlag(FlickDirection.UP))
                {
                    // 上フリックはできない前提
                    y = Mathf.Min(0, y);
                }
            }
            else
            {
                // 上下には動かない
                y = 0;
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

        private void Move(Vector2 moveDiff)
        {
            currentRectTransform().anchoredPosition = initalPos + moveDiff;

            // 作用を受けるcornerの位置も、上記のdiffをもとに動作させる
            switch (flickDir)
            {
                case FlickDirection.RIGHT | FlickDirection.LEFT:
                    CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + moveDiff;
                    CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + moveDiff;
                    break;
                case FlickDirection.RIGHT:
                    CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + moveDiff;
                    break;
                case FlickDirection.LEFT:
                    CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + moveDiff;
                    break;
                case FlickDirection.UP | FlickDirection.DOWN:
                    CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + moveDiff;
                    CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + moveDiff;
                    break;
                case FlickDirection.UP:
                    CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + moveDiff;
                    break;
                case FlickDirection.DOWN:
                    CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + moveDiff;
                    break;
                default:
                    Debug.LogError("unsupported flickDir:" + flickDir);
                    break;
            }
        }

        // 左、左、と上、下、どちらか一方のみがある時は制限された向きにのみmoveする。
        private void ApplyPositionLimitByDirection()
        {
            switch (flickDir)
            {
                // 左右移動が可能なフリック
                case FlickDirection.RIGHT | FlickDirection.LEFT:
                    // リミッターなし
                    break;

                // 右に向かっていくオンリーのフリック
                case FlickDirection.RIGHT:
                    if (currentRectTransform().anchoredPosition.x < initalPos.x)
                    {
                        currentRectTransform().anchoredPosition = new Vector2(initalPos.x, currentRectTransform().anchoredPosition.y);
                    }
                    break;

                // 左に向かっていくオンリーのフリック
                case FlickDirection.LEFT:
                    if (initalPos.x < currentRectTransform().anchoredPosition.x)
                    {
                        currentRectTransform().anchoredPosition = new Vector2(initalPos.x, currentRectTransform().anchoredPosition.y);
                    }
                    break;

                // 上下移動が可能なフリック
                case FlickDirection.UP | FlickDirection.DOWN:
                    // リミッターなし
                    break;

                // 上に向かっていくオンリーのフリック
                case FlickDirection.UP:
                    if (currentRectTransform().anchoredPosition.y < initalPos.y)
                    {
                        currentRectTransform().anchoredPosition = new Vector2(currentRectTransform().anchoredPosition.x, initalPos.y);
                    }
                    break;

                // 下に向かっていくオンリーのフリック
                case FlickDirection.DOWN:
                    if (initalPos.y < currentRectTransform().anchoredPosition.y)
                    {
                        currentRectTransform().anchoredPosition = new Vector2(currentRectTransform().anchoredPosition.x, initalPos.y);
                    }
                    break;
                default:
                    Debug.LogError("unsupported flickDir:" + flickDir);
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
                if (parentHandler != null)
                {
                    parentHandler.DisppearProgress(this, disappearProgress);
                }

                // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
                foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.DisppearProgress(disappearProgress);
                }

                // これから表示される要素のcontentsにappearProgressを伝える
                switch (currentMovedDirection)
                {
                    case FlickDirection.RIGHT:
                        AppearProgress(CornerFromLeft, appearProgress); break;
                    case FlickDirection.LEFT:
                        AppearProgress(CornerFromRight, appearProgress); break;
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
                if (parentHandler != null)
                {
                    parentHandler.DisppearProgress(this, disappearProgress);
                }

                // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
                foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.DisppearProgress(disappearProgress);
                }

                // これから表示される要素のcontentsにappearProgressを伝える
                switch (currentMovedDirection)
                {
                    case FlickDirection.UP:
                        AppearProgress(CornerFromBottom, appearProgress); break;
                    case FlickDirection.DOWN:
                        AppearProgress(CornerFromTop, appearProgress); break;
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
                    containedUIComponent.AppearProgress(progress);
                }
            }
        }

        // 出現度合いを上流に通知する
        private void SendAppearProgress(float progress)
        {
            if (parentHandler != null)
            {
                parentHandler.AppearProgress(this, progress);
            }
        }

        private FlickDirection DetermineFlickResult()
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

        public RectTransform[] CollectRelatedFlickableCorners()
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
        public static (bool isFound, GamenDriver driver) TryFindingAutoFlickRoute(FlickableCorner from, FlickableCorner to)
        {
            if (from == null)
            {
                return (false, null);
            }
            if (to == null)
            {
                return (false, null);
            }

            // 接続されていれば、経路が見つかってステップが返せるはず。
            if (IsConnected(from, to, out var direction, out var steps))
            {
                var driver = new GamenDriver(steps);
                return (true, driver);
            }

            return (false, null);
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

        // デバッグ用
        private void Show(string title, params (string key, object param)[] p)
        {
            var str = "title:" + title + "\n";
            foreach (var pi in p)
            {
                str += "    " + pi.key + ":" + pi.param + ", \n";
            }
            Debug.Log(str);
        }
    }
}