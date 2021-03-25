using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    /*
        フリック可能なCorner
        // TODO: 対象となるhandlerを見つけてinspectorに表示したい感じある。
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

        private IFlickableCornerHandler[] parentHandlers = new IFlickableCornerHandler[0];

        public new void Awake()
        {
            base.SetSubclassReloadAction(
                () =>
                {
                    // 親を探して、IFlickableCornerFocusHandlerを持っているオブジェクトがあったら、変更があるたびに上流に通知を流す。
                    if (transform.parent != null)
                    {
                        parentHandlers = transform.parent.GetComponents<IFlickableCornerHandler>();
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

            // 念の為取っておく、実機ビルドができたらチェックして、必要なくなったら消そう。
            // Show("OnPointerEnter",
            //     ("button", eventData.button),// PCだとleft、タップ系だと何が入るんだろう。これは無理っぽいな、、、

            //     ("clickCount", eventData.clickCount),//1
            //     ("clickTime", eventData.clickTime),// 開始時刻のseconds が floatで取れるっぽい。

            //     ("currentInputModule", eventData.currentInputModule),// 色々な情報が一発で出る、なるほど
            //     ("delta", eventData.delta),

            //     ("dragging", eventData.dragging),
            //     ("eligibleForClick", eventData.eligibleForClick),

            //     ("enterEventCamera", eventData.enterEventCamera),
            //     ("lastPress", eventData.lastPress),
            //     ("pointerClick", eventData.pointerClick),// nullになる
            //     ("pointerCurrentRaycast", eventData.pointerCurrentRaycast),
            //     ("pointerDrag", eventData.pointerDrag),// ここに現在ドラッグしている判定のオブジェクトが入る



            //     ("pointerEnter", eventData.pointerEnter),// ここに現在ドラッグしている判定のオブジェクトが入る、お、拾えるな、、



            //     ("pointerId", eventData.pointerId),
            //     ("pointerPress", eventData.pointerPress),
            //     ("pointerPressRaycast", eventData.pointerPressRaycast),
            //     ("position", eventData.position),
            //     ("pressEventCamera", eventData.pressEventCamera),
            //     ("pressPosition", eventData.pressPosition),
            //     ("rawPointerPress", eventData.rawPointerPress),
            //     ("selectedObject", eventData.selectedObject),
            //     ("used", eventData.used),
            //     ("useDragThreshold", eventData.useDragThreshold),// trueになっている。よくわからん

            //     ("scrollDelta", eventData.scrollDelta)// 実際のUIのスクロール距離、0,0から発生
            // );
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
            // Debug.Log("init this:" + this);
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
                    // 用意しているflickの種類でconstraintを切り替える。

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
                    ApplyConstraintToDir(flickDir, eventData);

                    // 動かす
                    Move(eventData.delta);

                    // 保持しているcornerと関連するcornerに対して、willDisappearとwillAppearを通知する。
                    NotifyAppearance(eventData.delta);
                    // TODO: この時に向かった方向とは逆の方向にdragし、なおかつ初期値を超えたタイミングで、キャンセルを流してなおかつ反対側にあるコンテンツがあればwillAppearを呼び出したい。

                    state = FlickState.BEGIN;
                    break;
                case FlickState.PROCESSING:
                case FlickState.CANCELLING:
                    // すでにend中なので無視する
                    return;
                default:
                    // イレギュラーなので解除
                    state = FlickState.NONE;
                    break;
            }
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
                    Debug.LogError("unhandled state:" + state);
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
            ApplyConstraintToDir(flickDir, eventData);

            // ドラッグ継続
            Move(eventData.delta);

            // 始めたflickが許可していない逆方向に突き抜けそう、みたいなのを抑制する
            ApplyPositionLimitByDirection();

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

            // // TODO: こいつらがキャンセルなのかなんなのかを見極める必要がある。と思ったけど付加情報ぽいな、、
            // {
            //     if (eventData.hovered.Count == 0)
            //     {
            //         // Debug.Log("end時にキャンバス外");
            //         // state = FlickState.ENDING;
            //         // DoneFlicked();
            //         // return;
            //     }
            //     else
            //     {
            //         // TODO: この辺のどこかを無視しないと、キャンバス内かつパーツ外が発生してやりにくい、、ポイント位置と実位置の距離でみるのが良さそう。
            //         if (eventData.hovered[0] != this.gameObject)
            //         {
            //             // Debug.Log("end時にhover下が別のもの:" + eventData.hovered[0]);
            //             // このオブジェクトより優先度が高いものの上に到達した
            //             // state = FlickState.ENDING;
            //             // DoneFlicked();
            //             // return;
            //         }
            //     }

            //     if (eventData.pointerDrag != this.gameObject)
            //     {
            //         // Debug.Log("end時に違うものをドラッグした状態で到達");
            //         // state = FlickState.ENDING;
            //         // DoneFlicked();
            //         // return;
            //     }
            // }

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
                            // TODO: 戻るところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。アニメーション用のやつを渡すか。

                            ResetToInitialPosition();

                            NotifyCancelled();

                            state = FlickState.NONE;
                            yield break;

                        // flick成立、目的位置への移動処理を行っている状態。
                        case FlickState.PROCESSING:
                            // TODO: 達成するところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。

                            SetToTargetPosition(flickedDir);

                            NotifyProcessed(flickedDir);

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

        IEnumerator animationCor;

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

        public float reactUnitSize;// 反応サイズ、これを超えたら要素をmoveUnitSizeまで移動させる。
        public float moveUnitSize;// 反応後移動サイズ
        // TODO: これがセットされていない場合、サイズから自動算出できるはず、from系がセットしてあれば、、、


        // ここに要素がセットしてあればその方向に動作する
        public Corner CornerFromLeft;
        public Corner CornerFromRight;
        public Corner CornerFromTop;
        public Corner CornerFromBottom;

        private Vector2 cornerFromLeftInitialPos;
        private Vector2 cornerFromRightInitialPos;
        private Vector2 cornerFromTopInitialPos;
        private Vector2 cornerFromBottomInitialPos;


        // フリック機能の状態
        // TODO: インターフェース化しそう
        // 自身が保持しているコンテンツに対する警告なので、要素がICornerを持っていたらwillDisappearとかが着火できる
        private void WillBeginFlick()
        {
            // Debug.Log("フリックを開始する dir:" + dir);
        }

        private void DidEndFlicked()
        {
            // Debug.Log("フリックを終了する dir:" + dir);
        }

        // willDisappearを自身のコンテンツに出し、willAppearを関連する方向のコンテンツに出す
        private void NotifyAppearance(Vector2 delta)
        {
            // 上位のハンドラがあればそれに対して消える予定を通知する
            foreach (var parentHandler in parentHandlers)
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
            foreach (var parentHandler in parentHandlers)
            {
                parentHandler.WillAppear(this);
            }
        }

        // disppearCancelledを自身のコンテンツに出し、appearCancelledを関連するコンテンツに出す
        private void NotifyCancelled()
        {
            // 上位のハンドラがあればそれに対して消える予定のキャンセルを通知する
            foreach (var parentHandler in parentHandlers)
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
            foreach (var parentHandler in parentHandlers)
            {
                parentHandler.AppearCancelled(this);
            }
        }

        // didDisappearを自身のコンテンツに出し、didAppearを関連する方向のコンテンツに出す
        private void NotifyProcessed(FlickDirection resultDir)
        {
            // 上位のハンドラがあればそれに対して消えたことを通知する
            foreach (var parentHandler in parentHandlers)
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
            foreach (var parentHandler in parentHandlers)
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
            foreach (var parentHandler in parentHandlers)
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
        private void ApplyConstraintToDir(FlickDirection dir, PointerEventData eventData)
        {
            var resultX = 0f;
            var resultY = 0f;

            // x
            {
                if (dir.HasFlag(FlickDirection.RIGHT))
                {
                    resultX = eventData.delta.x;
                }

                if (dir.HasFlag(FlickDirection.LEFT))
                {
                    resultX = eventData.delta.x;
                }
            }

            // y
            {
                if (dir.HasFlag(FlickDirection.DOWN))
                {
                    resultY = eventData.delta.y;
                }

                if (dir.HasFlag(FlickDirection.UP))
                {
                    resultY = eventData.delta.y;
                }
            }

            eventData.delta = new Vector2(resultX, resultY);
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

        // 結果的にどちらの方向にflickしたかで、自身と各コーナーの位置を最終目的地へとセットする
        private void SetToTargetPosition(FlickDirection resultDir)
        {
            // flickDir で変化させるが、まあどっちに向かっているかで最終値が違う。
            var moveByUnitSizeVec = Vector2.zero;

            // 結果値が必要になった。
            switch (resultDir)
            {
                case FlickDirection.RIGHT:
                    moveByUnitSizeVec = new Vector2(moveUnitSize, 0f);
                    break;
                case FlickDirection.LEFT:
                    moveByUnitSizeVec = new Vector2(-moveUnitSize, 0f);
                    break;
                default:
                    Debug.LogError("まだサポートしてない resultDir:" + resultDir);
                    break;
            }

            // 自身の位置を確定させる
            currentRectTransform().anchoredPosition = initalPos + moveByUnitSizeVec;

            // 関連cornerの位置を確定させる + 関連コーナーにさらに関連がある場合連動させる。
            // 自身を渡しているのは、関連する要素が保持しているfrom系のcornerがこれ自身な可能性があり、そうすると二重に移動されてしまうのを、自身と対象を比較させて不要な移動だけをキャンセルするため。
            if (CornerFromLeft != null)
            {
                CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + moveByUnitSizeVec;
                if (CornerFromLeft is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromLeft).UpdateRelatedCornerPositions(this, moveByUnitSizeVec);
                }
            }
            if (CornerFromRight != null)
            {
                CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + moveByUnitSizeVec;
                if (CornerFromRight is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromRight).UpdateRelatedCornerPositions(this, moveByUnitSizeVec);
                }
            }
            if (CornerFromTop != null)
            {
                CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + moveByUnitSizeVec;
                if (CornerFromTop is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromTop).UpdateRelatedCornerPositions(this, moveByUnitSizeVec);
                }
            }
            if (CornerFromBottom != null)
            {
                CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + moveByUnitSizeVec;
                if (CornerFromBottom is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromBottom).UpdateRelatedCornerPositions(this, moveByUnitSizeVec);
                }
            }
        }

        // TODO: この方法だとdeltaが足し算になってしまってこれは誤差がすごそう、、うーん、、、まあ実機でやってみよう -> やってみた感じ、フリック開始からちょっとだけでもいいから加速があった方がそれぽい。さてどうやって加速を作り出せるようにするかというと、速度か。精度はイマイチなので何かしら手を加えたいところ。加速度に応じた離れと吸着をやれば良さそう。
        // 本当はmove towardsな感じで、タッチ位置のbefore-afterをみるのが良さそう。
        // TODO: あとそもそもタッチポイントがダイレクトに領域をその動かす対象とするのが気持ち悪いんだよな、、、中間体があったほうがいい気がする。
        private void Move(Vector2 delta)
        {
            currentRectTransform().anchoredPosition += delta;

            var diff = currentRectTransform().anchoredPosition - initalPos;

            // 作用を受けるcornerの位置も、上記のdiffをもとに動作させる
            switch (flickDir)
            {
                case FlickDirection.RIGHT | FlickDirection.LEFT:
                    CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + diff;
                    CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + diff;
                    break;
                case FlickDirection.RIGHT:
                    CornerFromLeft.currentRectTransform().anchoredPosition = cornerFromLeftInitialPos + diff;
                    break;
                case FlickDirection.LEFT:
                    CornerFromRight.currentRectTransform().anchoredPosition = cornerFromRightInitialPos + diff;
                    break;
                case FlickDirection.UP | FlickDirection.DOWN:
                    CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + diff;
                    CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + diff;
                    break;
                case FlickDirection.UP:
                    CornerFromBottom.currentRectTransform().anchoredPosition = cornerFromBottomInitialPos + diff;
                    break;
                case FlickDirection.DOWN:
                    CornerFromTop.currentRectTransform().anchoredPosition = cornerFromTopInitialPos + diff;
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
                var progress = Mathf.Abs(xDist) / reactUnitSize;

                var appearProgress = Mathf.Min(1f, progress);
                var disappearProgress = Mathf.Max(0f, 1.0f - progress);

                // 上流があればdisappear度合いを伝える
                foreach (var parentHandler in parentHandlers)
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
                var progress = Mathf.Abs(yDist) / reactUnitSize;

                var appearProgress = Mathf.Min(1f, progress);
                var disappearProgress = Mathf.Max(0f, 1.0f - progress);

                // 上流があればdisappear度合いを伝える
                foreach (var parentHandler in parentHandlers)
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
            foreach (var parentHandler in parentHandlers)
            {
                parentHandler.AppearProgress(this, progress);
            }
        }

        private FlickDirection DetermineFlickResult()
        {
            var xDist = initalPos.x - currentRectTransform().anchoredPosition.x;
            var yDist = initalPos.y - currentRectTransform().anchoredPosition.y;

            if (flickDir.HasFlag(FlickDirection.RIGHT) && reactUnitSize <= -xDist)
            {
                return FlickDirection.RIGHT;
            }

            if (flickDir.HasFlag(FlickDirection.LEFT) && reactUnitSize <= xDist)
            {
                return FlickDirection.LEFT;
            }

            if (flickDir.HasFlag(FlickDirection.UP) && reactUnitSize <= -yDist)
            {
                return FlickDirection.UP;
            }

            if (flickDir.HasFlag(FlickDirection.DOWN) && reactUnitSize <= yDist)
            {
                return FlickDirection.DOWN;
            }

            // flick判定の結果、該当なし
            return FlickDirection.NONE;
        }

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

        // fromからtoへとつながる経路探索を行う
        public static (bool isFound, IEnumerator animation) TryFindingAutoFlickRoute(FlickableCorner from, FlickableCorner to)
        {
            // 接続されていれば、経路が見つかってステップが返せるはず。
            if (IsConnected(from, to, out var direction, out var steps))
            {
                // TODO: アニメーションの自動化、Driverを作る必要がある。まあ作るんだが。
                // Flickableに対して、特定の方向に、特定のペースで進む機構っていうのを作る。
                var driver = new GamenDriver(steps);

                IEnumerator animation()
                {
                    while (driver.MoveNext())
                    {
                        yield return null;
                    }
                };

                return (true, animation());
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