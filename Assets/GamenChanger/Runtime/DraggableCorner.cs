using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GamenChangerCore
{
    public class DraggableCorner : Corner
    {
        private enum DragState
        {
            NONE,
            INIT,
            BEGIN,
            DRAGGING,
            RELEASING
        }

        private DraggableAgent currentAgent;
        private DragState state;

        private IDraggableCornerHandler listener;

        public new void Awake()
        {
            var listenerCandidate = transform.parent.GetComponent<IDraggableCornerHandler>();
            if (listenerCandidate != null)
            {
                // pass.
            }
            else
            {
                Debug.LogError("this:" + gameObject + " 's DraggableCorner handler is not found in parent:" + transform.parent.gameObject + ". please set IDraggableCornerHandler to parent GameObject before addComponent or instantiate.");
                return;
            }

            // non nullなのを確認した上でセットする
            listener = listenerCandidate;

            // baseの初期化時に実行する関数としてこのクラスの初期化処理をセット
            base.SetSubclassReloadAction(
                () =>
                {
                    var whole = ExposureAllContents();

                    // containedUIComponentsへと自動的にagentを生やす
                    // TODO: agent勝手に付けるよりももっといい方法があると思うが、、まあはい。
                    foreach (var content in whole)
                    {
                        // すでにセット済み
                        if (content.gameObject.GetComponent<DraggableAgent>() != null)
                        {
                            continue;
                        }

                        var agent = content.gameObject.AddComponent<DraggableAgent>();
                        agent.parent = this;
                    }

                    // 親のOnInitializedを着火する
                    // TODO: handlerのinitializeを行う。
                    // listener.OnInitialized(
                    //     One,
                    //     whole.Select(s => s.gameObject).ToArray(),
                    //     newOne =>
                    //     {
                    //         // すでに同じオブジェクトが押された後であれば無視する
                    //         if (newOne == One)
                    //         {
                    //             return;
                    //         }

                    //         var before = One;

                    //         // Oneの更新
                    //         One = newOne;

                    //         var wholeContents = ExposureAllContents();

                    //         // 親のOnChangedByListenerを着火する
                    //         listener.OnChangedToOneByHandler(One, before, wholeContents.Select(t => t.gameObject).ToArray());
                    //     }
                    // );
                }
            );

            base.Awake();
        }

        private struct DeltaLimit
        {
            public readonly float TopSize;
            public readonly float RightSize;
            public readonly float BottomSize;
            public readonly float LeftSize;
            public DeltaLimit(RectTransform parent, RectTransform content)
            {
                /*
                    uGUI conrer内でのcontentの位置を知る必要がある。
                    親の幅からのアンカーで原点位置が変わる、pivotは重心。なので、x,y位置とかは、
                    centerX = 親のwidth * anchor min.x で決まる。
                    centerY = 親のheight * anchor min.y で決まる。
                
                    これがコンテンツの中心x,yになるので、ここからそれぞれコンテンツのwidth、heightをpivotの偏りだけ移動させた値が必要になる。
                */
                var contentCenterX = parent.sizeDelta.x * content.anchorMin.x;
                var contentCenterY = parent.sizeDelta.y * content.anchorMin.y;

                var contentLeftTopX = contentCenterX - content.pivot.x * content.sizeDelta.x;
                var contentLeftTopY = contentCenterY - content.pivot.y * content.sizeDelta.y;

                // 移動可能なサイズを収集する。
                this.TopSize = Mathf.Max(0, contentLeftTopY);
                this.RightSize = Mathf.Max(0, parent.sizeDelta.x - (contentLeftTopX + content.sizeDelta.x));
                this.BottomSize = Mathf.Max(0, parent.sizeDelta.y - (contentLeftTopY + content.sizeDelta.y));
                this.LeftSize = Mathf.Max(0, contentLeftTopX);
            }
        }

        private DeltaLimit deltaLimit;

        public void OnInitializePotentialDrag(DraggableAgent agent, PointerEventData eventData)
        {
            Debug.Log("OnInitializePotentialDrag, eventData:" + eventData);
            if (currentAgent == null)
            {
                // pass.
            }
            else
            {
                SetToNone();
                return;
            }

            switch (state)
            {
                case DragState.NONE:
                    // pass.
                    break;
                // 最終アニメーション動作中なので入力を無視する
                case DragState.RELEASING:
                    return;
                case DragState.INIT:
                    // 含まれるボタンを押したりすると発生する。無視していいやつ。
                    break;
                default:
                    Debug.LogError("OnInitializePotentialDrag unhandled state:" + state);
                    SetToNone();
                    return;
            }

            // 開始
            state = DragState.INIT;
            currentAgent = agent;
            // TODO: NotifyTouch();
        }

        public void OnBeginDrag(DraggableAgent agent, PointerEventData eventData)
        {
            Debug.Log("OnBeginDrag");
            if (currentAgent == null)
            {
                SetToNone();
                return;
            }

            if (currentAgent != agent)
            {
                Debug.Log("OnBeginDrag currentが切り替わった。");
                SetToNone();
                return;
            }

            switch (state)
            {
                case DragState.INIT:
                    // pass.
                    break;
                case DragState.RELEASING:
                    // すでにend中なので無視する
                    return;
                case DragState.NONE:
                    return;
                default:
                    // イレギュラーなので解除
                    SetToNone();
                    return;
            }

            // 対象物のサイズとこのオブジェクトのサイズに合わせて移動可能な範囲を出し、deltaを制限する。
            var currentDraggableRect = agent.GetComponent<RectTransform>();
            deltaLimit = new DeltaLimit(currentRectTransform(), currentDraggableRect);

            var delta = eventData.delta;
            LimitDeltaValie(eventData, deltaLimit);
            Debug.Log("eventData.delta:" + eventData.delta);

            // // 利用できる方向がない場合、無効化する。
            // if (availableFlickDir == FlickDirection.NONE)
            // {
            //     state = FlickState.NONE;
            //     return;
            // }

            // flickDir = availableFlickDir;

            // // from系の初期位置を保持
            // UpdateInitialPos();

            // // eventDataのパラメータを上書きし、指定のオブジェクトをドラッグしている状態に拘束する
            // ApplyConstraintToDir(flickDir, eventData);

            // // 動かす
            // Move(eventData.delta);

            // // 保持しているcornerと関連するcornerに対して、willDisappearとwillAppearを通知する。
            // NotifyAppearance(eventData.delta);
            // // TODO: この時に向かった方向とは逆の方向にdragし、なおかつ初期値を超えたタイミングで、キャンセルを流してなおかつ反対側にあるコンテンツがあればwillAppearを呼び出したい。

            state = DragState.BEGIN;
        }

        public void OnDrag(DraggableAgent agent, PointerEventData eventData)
        {
            // Debug.Log("OnDrag");
            if (currentAgent == null)
            {
                SetToNone();
                return;
            }

            if (currentAgent != agent)
            {
                Debug.Log("OnDrag currentが切り替わった。");
                SetToNone();
                return;
            }

            // 特定状態以外無視する
            switch (state)
            {
                case DragState.BEGIN:
                    state = DragState.DRAGGING;
                    // pass.
                    break;
                case DragState.DRAGGING:
                    // pass.
                    break;
                case DragState.NONE:
                    // 無視する
                    return;
                case DragState.RELEASING:
                    // すでにend中なので無視する
                    return;
                default:
                    Debug.LogError("unhandled state:" + state);
                    SetToNone();
                    return;
            }

            // drag処理を行う

            // TODO: なんかする
            // // drag対象がついて行ってない状態なので、終了させるという条件が必要になる。
            // // 画面外にdragしたら終了
            // if (0 < eventData.position.x && eventData.position.x < Screen.width && 0 < eventData.position.y && eventData.position.y < Screen.height)
            // {
            //     // pass.
            // }
            // else
            // {
            //     // 画面外にタッチが飛び出したので、drag終了する。
            //     OnEndDrag(eventData);
            //     return;
            // }

            // // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
            // if (eventData.pointerDrag != this.gameObject)
            // {
            //     // TODO: これって発生するのかな、、
            //     Debug.Log("drag ハズレ3");
            // }

            // // eventDataのパラメータを上書きし、指定のオブジェクトをドラッグしている状態に拘束する
            // ApplyConstraintToDir(flickDir, eventData);

            // // ドラッグ継続
            // Move(eventData.delta);

            // // 始めたflickが許可していない逆方向に突き抜けそう、みたいなのを抑制する
            // ApplyPositionLimitByDirection();

            // var actualMoveDir = DetectFlickingDirection(eventData.delta);

            // //progressの更新を行う
            // UpdateProgress(actualMoveDir);
        }

        public void OnEndDrag(DraggableAgent agent, PointerEventData eventData)
        {
            // Debug.Log("OnEndDrag");
            if (currentAgent == null)
            {
                SetToNone();
                return;
            }

            if (currentAgent != agent)
            {
                Debug.Log("OnEndDrag currentが切り替わった。");
                SetToNone();
                return;
            }

            // 特定状態以外無視する
            switch (state)
            {
                case DragState.DRAGGING:
                    // pass.
                    break;
                case DragState.NONE:
                    // 無視する
                    return;
                case DragState.RELEASING:
                    // すでにend中なので無視する
                    return;
                default:
                    Debug.LogError("unhandled state:" + state);
                    SetToNone();
                    return;
            }

            // 正常にdragの終了に到達した
            // TODO: なんかする

            // // flickが発生したかどうかチェックし、発生していれば移動を完了させる処理モードに入る。
            // // そうでなければ下の位置に戻すキャンセルモードに入る。
            // var flickedDir = DetermineFlickResult();

            // // progressの更新を行う
            // UpdateProgress(flickedDir);

            // リリース中状態にする
            state = DragState.RELEASING;

            // enumeratorを回してアニメーション中状態を処理する
            IEnumerator animationCor()
            {
                while (true)
                {
                    switch (state)
                    {
                        // 終了処理中
                        case DragState.RELEASING:
                            // TODO: 達成するところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。

                            // SetToTargetPosition(flickedDir);

                            // NotifyProcessed(flickedDir);

                            SetToNone();
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

        private IEnumerator animationCor;

        private void SetToNone()
        {
            state = DragState.NONE;
            currentAgent = null;
        }

        private void Update()
        {
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

        private void LimitDeltaValie(PointerEventData eventData, DeltaLimit deltaLimit)
        {
            var x = 0f;
            var y = 0f;

            if (eventData.delta.x < 0)
            {
                x = -Mathf.Min(Mathf.Abs(eventData.delta.x), deltaLimit.LeftSize);
            }
            else if (0 < eventData.delta.x)
            {
                x = Mathf.Min(eventData.delta.x, deltaLimit.RightSize);
            }

            if (eventData.delta.y < 0)
            {
                y = -Mathf.Min(Mathf.Abs(eventData.delta.y), deltaLimit.TopSize);
            }
            else if (0 < eventData.delta.y)
            {
                y = Mathf.Min(eventData.delta.y, deltaLimit.BottomSize);
            }

            eventData.delta = new Vector2(x, y);
        }
    }
}