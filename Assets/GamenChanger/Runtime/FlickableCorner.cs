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
            IN,
            BEGIN,
            FLICKING,
            ENDING,
            ENDED
        }

        private FlickState state = FlickState.NONE;
        private FlickDirection flickDir;

        // 開始条件を判定するイベント
        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            switch (state)
            {
                case FlickState.NONE:
                    // pass.
                    break;
                case FlickState.ENDING:
                    Debug.Log("ending中にtouchが来た");
                    break;
                case FlickState.IN:
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
            state = FlickState.IN;
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
                case FlickState.IN:
                    // 用意しているflickの種類でconstraintを切り替える。
                    // TODO: この時点で位置の変動が発生する。移動距離みたいなのが生まれるのもここなのかな。とりあえず自分自身を移動開始する。

                    var delta = eventData.delta;
                    // Debug.Log("delta:" + delta);// 左で-、下で-、右で+、上で+
                    flickDir = GetAvailableDirection();
                    ApplyConstraintToDir(flickDir, eventData);
                    ResetPosition();
                    ApplyDeltaPosition(eventData.delta);
                    state = FlickState.BEGIN;

                    WillBeginFlick(flickDir);
                    break;
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
            // TODO: この辺のどこかを無視しないとやりにくい
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
                default:
                    Debug.LogError("unhandled state:" + state);
                    state = FlickState.NONE;
                    return;
            }

            // drag対象がついて行ってない状態なので、終了。
            if (eventData.hovered.Count == 0)
            {
                Debug.Log("ハズレ1");
                state = FlickState.ENDING;
                return;
            }

            // このオブジェクトより優先度が高いものの上に到達したので、ドラッグの解除を行う
            if (eventData.hovered[0] != this.gameObject)
            {
                Debug.Log("ハズレ2");
                state = FlickState.ENDING;
                return;
            }

            // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
            if (eventData.pointerDrag != this.gameObject)
            {
                Debug.Log("ハズレ3");
                state = FlickState.ENDING;
                return;
            }

            // 指定のオブジェクトをドラッグしている状態
            ApplyConstraintToDir(flickDir, eventData);

            // ドラッグ継続
            ApplyDeltaPosition(eventData.delta);

            // 始めたflickが逆方向に突き抜けそう、みたいなのを抑制する
            ApplyPositionLimitByDirection();

            // 距離計測
            var xLength = flickStartPosition.x - currentRectTransform.anchoredPosition.x;
            var yLength = flickStartPosition.y - currentRectTransform.anchoredPosition.y;

            var xSize = Mathf.Abs(xLength);
            var ySize = Mathf.Abs(yLength);

            if (flickDir.HasFlag(FlickDirection.RIGHT) || flickDir.HasFlag(FlickDirection.LEFT))
            {
                if (reactUnitSize <= xSize)
                {
                    Debug.Log("ここで離したらもうイベントを満たしたと言える x:" + xSize);
                }
            }
            if (flickDir.HasFlag(FlickDirection.UP) || flickDir.HasFlag(FlickDirection.DOWN))
            {
                if (reactUnitSize <= ySize)
                {
                    Debug.Log("ここで離したらもうイベントを満たしたと言える y:" + ySize);
                }
            }

            // Show("OnDrag",
            //     // ("button", eventData.button),// PCだとleft、タップ系だと何が入るんだろう。
            //     // ("clickCount", eventData.clickCount),//1
            //     // ("clickTime", eventData.clickTime),// 開始時刻のseconds が floatで取れるっぽい。

            //     // ("currentInputModule", eventData.currentInputModule),
            //     ("delta", eventData.delta),

            //     // ("dragging", eventData.dragging),
            //     // ("eligibleForClick", eventData.eligibleForClick),

            //     // ("enterEventCamera", eventData.enterEventCamera),
            //     // ("lastPress", eventData.lastPress),
            //     // ("pointerClick", eventData.pointerClick),
            //     // ("pointerCurrentRaycast", eventData.pointerCurrentRaycast),
            //     ("pointerDrag", eventData.pointerDrag),// ここに現在ドラッグしている判定のオブジェクトが入る
            //     ("buttonpointerEnter", eventData.pointerEnter),// ここに現在ドラッグしている判定のオブジェクトが入る

            //     // ("pointerId", eventData.pointerId),// -1
            //     // ("pointerPress", eventData.pointerPress),
            //     // ("pointerPressRaycast", eventData.pointerPressRaycast),
            //     // ("position", eventData.position),// グローバルな位置
            //     // ("pressEventCamera", eventData.pressEventCamera),
            //     // ("pressPosition", eventData.pressPosition),グローバルな開始位置かな、UI上の値ではなさそう。
            //     // ("rawPointerPress", eventData.rawPointerPress),
            //     // ("selectedObject", eventData.selectedObject),
            //     // ("used", eventData.used),// false
            //     // ("useDragThreshold", eventData.useDragThreshold)// trueになっている。よくわからん

            //     ("scrollDelta", eventData.scrollDelta)// 実際のUIのスクロール距離、0,0から発生)
            // );
        }

        // 終了時イベント
        public void OnEndDrag(PointerEventData eventData)
        {
            // 特定状態以外無視する
            // TODO: この辺のどこかを無視しないとやりにくい
            switch (state)
            {
                case FlickState.FLICKING:
                    // pass.
                    break;
                case FlickState.NONE:
                    // 無視する
                    return;
                case FlickState.ENDING:
                    // すでにend中なので無視する
                    return;
                default:
                    Debug.LogError("unhandled state:" + state);
                    state = FlickState.NONE;
                    return;
            }

            if (eventData.hovered.Count == 0)
            {
                // キャンバス外に到達
                state = FlickState.ENDING;
                DidEndFlicked(flickDir);
                return;
            }

            if (eventData.hovered[0] != this.gameObject)
            {
                // このオブジェクトより優先度が高いものの上に到達したので、ドラッグの解除を行う
                state = FlickState.ENDING;
                DidEndFlicked(flickDir);
                return;
            }

            if (eventData.pointerDrag != this.gameObject)
            {
                // 違うものをドラッグした状態で到達
                state = FlickState.ENDING;
                DidEndFlicked(flickDir);
                return;
            }

            state = FlickState.ENDING;
            // TODO: アニメーションで戻るという状態が存在するはずで、その最中に移動開始されるとかはありそうな気がする。その時、オリジナルの起点を持っておかないといけない。つまりこれは「まだ状態を保持している」ことを意味する。これをどう加味するか。
            DidEndFlicked(flickDir);
        }

        private void Update()
        {
            switch (state)
            {
                case FlickState.ENDING:
                    // TODO: 戻るところのアニメーションはなんか自由に頑張ってくれってやりたいんだよな、どうするかな。
                    ResetToDefaultPosition();
                    state = FlickState.ENDED;
                    // TODO: ENDED用意してみたが、とりあえずすぐNONEにしちゃう。
                    state = FlickState.NONE;
                    break;
                default:
                    // 何もしない
                    break;
            }
        }


        public float reactUnitSize;// 反応サイズ、これを超えたら要素をmoveUnitSizeまで移動させる。
        public float moveUnitSize;// 反応後移動サイズ
        // TODO: これを自動算出できるはず、from系がセットしてあれば、、、


        // ここに要素がセットしてあればその方向に動作する
        public Corner CornerFromLeft;
        public Corner CornerFromRight;
        public Corner CornerFromTop;
        public Corner CornerFromBottom;


        // フリック機能の状態
        // TODO: インターフェース化しそう
        private void WillBeginFlick(FlickDirection dir)
        {
            Debug.Log("フリックを開始する dir:" + dir);
        }

        private void DidEndFlicked(FlickDirection dir)
        {
            Debug.Log("フリックを終了する dir:" + dir);
        }
        // TODO: このへんはFlickを離した後のアニメーションとかにも影響しそう


        private enum FlickDirection
        {
            NONE = 0,
            UP = 0x001,
            RIGHT = 0x002,
            DOWN = 0x004,
            LEFT = 0x008
        }

        // 移動可能な方向を返す
        // TODO: deltaを使えば、「最初に行こうとした方向以外に行かない」ができるが、制約なので後で考えよう。
        private FlickDirection GetAvailableDirection()
        {
            var dir = FlickDirection.NONE;
            if (CornerFromLeft != null)
            {
                dir = dir | FlickDirection.RIGHT;
            }
            if (CornerFromRight != null)
            {
                dir = dir | FlickDirection.LEFT;
            }
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

        private Vector2 flickStartPosition;
        private void ResetPosition()
        {
            flickStartPosition = currentRectTransform.anchoredPosition;
        }

        private void ResetToDefaultPosition()
        {
            currentRectTransform.anchoredPosition = flickStartPosition;
        }

        // TODO: この方法だとdeltaが足し算になってしまってこれは誤差がすごそう、、うーん、、、まあ実機でやってみよう
        // 本当はmove towardsな感じで、タッチ位置をみるのが良さそう。
        private void ApplyDeltaPosition(Vector2 delta)
        {
            currentRectTransform.anchoredPosition += delta;
        }

        private void ApplyPositionLimitByDirection()
        {
            // 右に向かっていくオンリーのフリック
            if (flickDir.HasFlag(FlickDirection.RIGHT) && !flickDir.HasFlag(FlickDirection.LEFT))
            {
                if (currentRectTransform.anchoredPosition.x < flickStartPosition.x)
                {
                    currentRectTransform.anchoredPosition = new Vector2(flickStartPosition.x, currentRectTransform.anchoredPosition.y);
                }
            }
            // 左に向かっていくオンリーのフリック
            else if (flickDir.HasFlag(FlickDirection.LEFT) && !flickDir.HasFlag(FlickDirection.RIGHT))
            {
                if (flickStartPosition.x < currentRectTransform.anchoredPosition.x)
                {
                    currentRectTransform.anchoredPosition = new Vector2(flickStartPosition.x, currentRectTransform.anchoredPosition.y);
                }
            }

            // 上に向かっていくオンリーのフリック
            if (flickDir.HasFlag(FlickDirection.UP) && !flickDir.HasFlag(FlickDirection.DOWN))
            {
                if (currentRectTransform.anchoredPosition.y < flickStartPosition.y)
                {
                    currentRectTransform.anchoredPosition = new Vector2(currentRectTransform.anchoredPosition.x, flickStartPosition.y);
                }
            }
            // 下に向かっていくオンリーのフリック
            else if (flickDir.HasFlag(FlickDirection.DOWN) && !flickDir.HasFlag(FlickDirection.UP))
            {
                if (flickStartPosition.y < currentRectTransform.anchoredPosition.y)
                {
                    currentRectTransform.anchoredPosition = new Vector2(currentRectTransform.anchoredPosition.x, flickStartPosition.y);
                }
            }
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