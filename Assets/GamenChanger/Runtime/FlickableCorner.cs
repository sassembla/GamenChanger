using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    /*
        フリック可能なCorner
    */
    public class FlickableCorner : Corner, IDragHandler, IEndDragHandler, IInitializePotentialDragHandler, IBeginDragHandler
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


        // 開始条件を判定するイベント
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

                    ApplyConstraintToDir(flickDir, eventData);

                    // 動かす
                    Move(eventData.delta);

                    state = FlickState.BEGIN;

                    WillBeginFlick(flickDir);
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

            // drag対象がついて行ってない状態なので、終了。
            if (eventData.hovered.Count == 0)
            {
                // Debug.Log("drag中にオブジェクト外に行った");
            }
            else
            {
                // このオブジェクトより優先度が高いものの上に到達したので、ドラッグの解除を行う
                // このhoveredの順位は直前まで何を触っていたのかで変動することがあり、下地となるキャンバスの順位が変動する。
                // このGOをdragしていてもcanvas, GOの順になることがあり、すげー怖いがこれで動く。
                if (eventData.hovered[0] != this.gameObject && eventData.hovered[1] != this.gameObject)
                {
                    // Debug.Log("drag中にオブジェクト外に行った2");
                }
            }

            // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
            if (eventData.pointerDrag != this.gameObject)
            {
                Debug.Log("ハズレ3");
                state = FlickState.CANCELLING;
                return;
            }

            // 指定のオブジェクトをドラッグしている状態に拘束する
            ApplyConstraintToDir(flickDir, eventData);

            // ドラッグ継続
            Move(eventData.delta);

            // 始めたflickが逆方向に突き抜けそう、みたいなのを抑制する
            ApplyPositionLimitByDirection();

            //progressの更新を行う
            UpdateProgress();
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

            // progressの更新を行う
            UpdateProgress();

            // flickが発生したかどうかチェックし、発生していれば移動を完了させる処理モードに入る。
            // そうでなければ下の位置に戻すキャンセルモードに入る。
            var resultDir = DetermineFlickResult();
            var isFlicked = resultDir != FlickDirection.NONE;

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

                            state = FlickState.NONE;
                            yield break;

                        // flick成立、目的位置への移動処理を行っている状態。
                        case FlickState.PROCESSING:
                            // TODO: 達成するところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。

                            SetToTargetPosition(resultDir);

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

        private void Update()
        {
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
        private void WillBeginFlick(FlickDirection dir)
        {
            // Debug.Log("フリックを開始する dir:" + dir);
        }

        private void DidEndFlicked(FlickDirection dir)
        {
            // Debug.Log("フリックを終了する dir:" + dir);
        }


        private enum FlickDirection
        {
            NONE = 0,
            UP = 0x001,
            RIGHT = 0x002,
            DOWN = 0x004,
            LEFT = 0x008
        }

        // 移動可能な方向を返す
        private FlickDirection GetAvailableDirection(Vector2 deltaMove)
        {
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
            initalPos = currentRectTransform.anchoredPosition;

            if (CornerFromLeft != null)
            {
                cornerFromLeftInitialPos = CornerFromLeft.currentRectTransform.anchoredPosition;
            }
            if (CornerFromRight != null)
            {
                cornerFromRightInitialPos = CornerFromRight.currentRectTransform.anchoredPosition;
            }
            if (CornerFromTop != null)
            {
                cornerFromTopInitialPos = CornerFromTop.currentRectTransform.anchoredPosition;
            }
            if (CornerFromBottom != null)
            {
                cornerFromBottomInitialPos = CornerFromBottom.currentRectTransform.anchoredPosition;
            }
        }

        // 自身と各コーナーの位置を初期位置に戻す
        private void ResetToInitialPosition()
        {
            currentRectTransform.anchoredPosition = initalPos;

            if (CornerFromLeft != null)
            {
                CornerFromLeft.currentRectTransform.anchoredPosition = cornerFromLeftInitialPos;
            }
            if (CornerFromRight != null)
            {
                CornerFromRight.currentRectTransform.anchoredPosition = cornerFromRightInitialPos;
            }
            if (CornerFromTop != null)
            {
                CornerFromTop.currentRectTransform.anchoredPosition = cornerFromTopInitialPos;
            }
            if (CornerFromBottom != null)
            {
                CornerFromBottom.currentRectTransform.anchoredPosition = cornerFromBottomInitialPos;
            }
        }

        // 結果的にどちらの方向にflick下かで、自身と各コーナーの位置を最終目的地へとセットする
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
            currentRectTransform.anchoredPosition = initalPos + moveByUnitSizeVec;

            // 関連cornerの位置を確定させる + 関連コーナーにさらに関連がある場合連動させる。
            // 自身を渡しているのは、関連する要素が保持しているfrom系のcornerがこれ自身な可能性があり、そうすると二重に移動されてしまうのを、自身と対象を比較させて不要な移動だけをキャンセルするため。
            if (CornerFromLeft != null)
            {
                CornerFromLeft.currentRectTransform.anchoredPosition = cornerFromLeftInitialPos + moveByUnitSizeVec;
                if (CornerFromLeft is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromLeft).UpdateRelatedCornerPositions(this, resultDir, moveByUnitSizeVec);
                }
            }
            if (CornerFromRight != null)
            {
                CornerFromRight.currentRectTransform.anchoredPosition = cornerFromRightInitialPos + moveByUnitSizeVec;
                if (CornerFromRight is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromRight).UpdateRelatedCornerPositions(this, resultDir, moveByUnitSizeVec);
                }
            }
            if (CornerFromTop != null)
            {
                CornerFromTop.currentRectTransform.anchoredPosition = cornerFromTopInitialPos + moveByUnitSizeVec;
                if (CornerFromTop is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromTop).UpdateRelatedCornerPositions(this, resultDir, moveByUnitSizeVec);
                }
            }
            if (CornerFromBottom != null)
            {
                CornerFromBottom.currentRectTransform.anchoredPosition = cornerFromBottomInitialPos + moveByUnitSizeVec;
                if (CornerFromBottom is FlickableCorner)
                {
                    ((FlickableCorner)CornerFromBottom).UpdateRelatedCornerPositions(this, resultDir, moveByUnitSizeVec);
                }
            }
        }

        // TODO: この方法だとdeltaが足し算になってしまってこれは誤差がすごそう、、うーん、、、まあ実機でやってみよう
        // 本当はmove towardsな感じで、タッチ位置のbefore-afterをみるのが良さそう。
        // TODO: あとそもそもタッチポイントがダイレクトに領域をその動かす対象とするのが気持ち悪いんだよな、、、中間体があったほうがいい気がする。
        private void Move(Vector2 delta)
        {
            currentRectTransform.anchoredPosition += delta;

            var diff = currentRectTransform.anchoredPosition - initalPos;

            // 作用を受けるcornerの位置も、上記のdiffをもとに動作させる
            switch (flickDir)
            {
                case FlickDirection.RIGHT | FlickDirection.LEFT:
                    CornerFromLeft.currentRectTransform.anchoredPosition = cornerFromLeftInitialPos + diff;
                    CornerFromRight.currentRectTransform.anchoredPosition = cornerFromRightInitialPos + diff;
                    break;
                case FlickDirection.RIGHT:
                    CornerFromLeft.currentRectTransform.anchoredPosition = cornerFromLeftInitialPos + diff;
                    break;
                case FlickDirection.LEFT:
                    CornerFromRight.currentRectTransform.anchoredPosition = cornerFromRightInitialPos + diff;
                    break;
                case FlickDirection.UP | FlickDirection.DOWN:
                    CornerFromBottom.currentRectTransform.anchoredPosition = cornerFromBottomInitialPos + diff;
                    CornerFromTop.currentRectTransform.anchoredPosition = cornerFromTopInitialPos + diff;
                    break;
                case FlickDirection.UP:
                    CornerFromBottom.currentRectTransform.anchoredPosition = cornerFromBottomInitialPos + diff;
                    break;
                case FlickDirection.DOWN:
                    CornerFromTop.currentRectTransform.anchoredPosition = cornerFromTopInitialPos + diff;
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
                    if (currentRectTransform.anchoredPosition.x < initalPos.x)
                    {
                        currentRectTransform.anchoredPosition = new Vector2(initalPos.x, currentRectTransform.anchoredPosition.y);
                    }
                    break;

                // 左に向かっていくオンリーのフリック
                case FlickDirection.LEFT:
                    if (initalPos.x < currentRectTransform.anchoredPosition.x)
                    {
                        currentRectTransform.anchoredPosition = new Vector2(initalPos.x, currentRectTransform.anchoredPosition.y);
                    }
                    break;

                // 上下移動が可能なフリック
                case FlickDirection.UP | FlickDirection.DOWN:
                    // リミッターなし
                    break;

                // 上に向かっていくオンリーのフリック
                case FlickDirection.UP:
                    if (currentRectTransform.anchoredPosition.y < initalPos.y)
                    {
                        currentRectTransform.anchoredPosition = new Vector2(currentRectTransform.anchoredPosition.x, initalPos.y);
                    }
                    break;

                // 下に向かっていくオンリーのフリック
                case FlickDirection.DOWN:
                    if (initalPos.y < currentRectTransform.anchoredPosition.y)
                    {
                        currentRectTransform.anchoredPosition = new Vector2(currentRectTransform.anchoredPosition.x, initalPos.y);
                    }
                    break;
                default:
                    Debug.LogError("unsupported flickDir:" + flickDir);
                    break;
            }
        }


        private void UpdateProgress()
        {
            // 横方向
            {
                var xDist = initalPos.x - currentRectTransform.anchoredPosition.x;

                var progress = Mathf.Abs(xDist) / reactUnitSize;

                var appearProgress = Mathf.Min(1f, progress);
                var disappearProgress = Mathf.Max(0f, 1.0f - progress);

                // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
                foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.DisppearProgress(disappearProgress);
                }

                // これから表示される要素のcontentsにappearProgressを伝える
                switch (flickDir)
                {
                    case FlickDirection.RIGHT:
                        foreach (var containedUIComponent in CornerFromLeft.GetComponentsInChildren<ICornerContent>())
                        {
                            containedUIComponent.AppearProgress(appearProgress);
                        }
                        break;
                    case FlickDirection.LEFT:
                        foreach (var containedUIComponent in CornerFromRight.GetComponentsInChildren<ICornerContent>())
                        {
                            containedUIComponent.AppearProgress(appearProgress);
                        }
                        break;
                }
            }

            // 縦方向
            {
                var yDist = initalPos.y - currentRectTransform.anchoredPosition.y;

                var progress = Mathf.Abs(yDist) / reactUnitSize;

                var appearProgress = Mathf.Min(1f, progress);
                var disappearProgress = Mathf.Max(0f, 1.0f - progress);

                // 自身の持っているcontentsに対応のものがあればdisappearProgressを伝える
                foreach (var containedUIComponent in this.GetComponentsInChildren<ICornerContent>())
                {
                    containedUIComponent.DisppearProgress(disappearProgress);
                }

                // これから表示される要素のcontentsにappearProgressを伝える
                switch (flickDir)
                {
                    case FlickDirection.UP:
                        foreach (var containedUIComponent in CornerFromTop.GetComponentsInChildren<ICornerContent>())
                        {
                            containedUIComponent.AppearProgress(appearProgress);
                        }
                        break;
                    case FlickDirection.DOWN:
                        foreach (var containedUIComponent in CornerFromBottom.GetComponentsInChildren<ICornerContent>())
                        {
                            containedUIComponent.AppearProgress(appearProgress);
                        }
                        break;
                }
            }
        }

        private FlickDirection DetermineFlickResult()
        {
            var xDist = initalPos.x - currentRectTransform.anchoredPosition.x;
            var yDist = initalPos.y - currentRectTransform.anchoredPosition.y;

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

        private void UpdateRelatedCornerPositions(FlickableCorner flickedSource, FlickDirection relatedFlickCornerFlickedDir, Vector2 movedVector)
        {
            switch (relatedFlickCornerFlickedDir)
            {
                case FlickDirection.RIGHT:
                    // 右方向へのフリックが完了したので、このコンテンツは左端から出現させられた。そのため、上下と左のコンテンツの位置を変更する。
                    if (CornerFromLeft != null && CornerFromLeft != flickedSource)
                    {
                        CornerFromLeft.currentRectTransform.anchoredPosition = CornerFromLeft.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromTop != null && CornerFromTop != flickedSource)
                    {
                        CornerFromTop.currentRectTransform.anchoredPosition = CornerFromTop.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromBottom != null && CornerFromBottom != flickedSource)
                    {
                        CornerFromBottom.currentRectTransform.anchoredPosition = CornerFromBottom.currentRectTransform.anchoredPosition + movedVector;
                    }
                    break;
                case FlickDirection.LEFT:
                    // 左方向へのフリックが完了したので、このコンテンツは右端から出現させられた。そのため、上下と右のコンテンツの位置を変更する。
                    if (CornerFromRight != null && CornerFromRight != flickedSource)
                    {
                        CornerFromRight.currentRectTransform.anchoredPosition = CornerFromRight.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromTop != null && CornerFromTop != flickedSource)
                    {
                        CornerFromTop.currentRectTransform.anchoredPosition = CornerFromTop.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromBottom != null && CornerFromBottom != flickedSource)
                    {
                        CornerFromBottom.currentRectTransform.anchoredPosition = CornerFromBottom.currentRectTransform.anchoredPosition + movedVector;
                    }
                    break;
                case FlickDirection.UP:
                    // 上方向へのフリックが完了したので、このコンテンツは下端から出現させられた。そのため、下と左右のコンテンツの位置を変更する。
                    if (CornerFromLeft != null && CornerFromLeft != flickedSource)
                    {
                        CornerFromLeft.currentRectTransform.anchoredPosition = CornerFromLeft.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromRight != null && CornerFromRight != flickedSource)
                    {
                        CornerFromRight.currentRectTransform.anchoredPosition = CornerFromRight.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromBottom != null && CornerFromBottom != flickedSource)
                    {
                        CornerFromBottom.currentRectTransform.anchoredPosition = CornerFromBottom.currentRectTransform.anchoredPosition + movedVector;
                    }
                    break;
                case FlickDirection.DOWN:
                    // 下方向へのフリックが完了したので、このコンテンツは上端から出現させられた。そのため、上と左右のコンテンツの位置を変更する。
                    if (CornerFromLeft != null && CornerFromLeft != flickedSource)
                    {
                        CornerFromLeft.currentRectTransform.anchoredPosition = CornerFromLeft.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromRight != null && CornerFromRight != flickedSource)
                    {
                        CornerFromRight.currentRectTransform.anchoredPosition = CornerFromRight.currentRectTransform.anchoredPosition + movedVector;
                    }
                    if (CornerFromTop != null && CornerFromTop != flickedSource)
                    {
                        CornerFromTop.currentRectTransform.anchoredPosition = CornerFromTop.currentRectTransform.anchoredPosition + movedVector;
                    }
                    break;
            }
        }

        // デバッグ用
        // private void Show(string title, params (string key, object param)[] p)
        // {
        //     var str = "title:" + title + "\n";
        //     foreach (var pi in p)
        //     {
        //         str += "    " + pi.key + ":" + pi.param + ", \n";
        //     }
        //     Debug.Log(str);
        // }
    }
}