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
            END
        }

        private FlickState state = FlickState.NONE;

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            // Debug.Log("OnInitializePotentialDrag");
            // pointer downだこれ。
            // foreach (var a in eventData.hovered)
            // {
            //     Debug.Log("a:" + a);
            // }

            /*
                これ自体、
                キャンバス、
                その他、、という順番に出てくる。で、範囲内でない場合は着火しない。
            */
            state = FlickState.IN;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            switch (state)
            {
                case FlickState.IN:
                    Debug.Log("begin!");

                    // 用意しているflickの種類でconstraintを切り替える。
                    // TODO: この時点で位置の変動が発生する。移動距離みたいなのが生まれるのもここなのかな。とりあえず自分自身を移動開始する。
                    state = FlickState.BEGIN;
                    break;
                default:
                    // イレギュラーなので解除
                    state = FlickState.NONE;
                    break;
            }
        }

        /*
            フリック開始したら移動を開始して、離したら戻る、みたいなのをやる。
        */

        public void OnDrag(PointerEventData eventData)
        {
            // drag対象がついて行ってない状態なので、終了。
            if (eventData.hovered.Count == 0)
            {
                state = FlickState.NONE;
                return;
            }

            if (eventData.hovered[0] != this.gameObject)
            {
                // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
                state = FlickState.NONE;
                return;
            }

            // ドラッグ継続
            Debug.Log("OnDrag");
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.hovered.Count == 0)
            {
                state = FlickState.NONE;
                return;
            }

            if (eventData.hovered[0] != this.gameObject)
            {
                // このオブジェクトではないものの上に到達したので、ドラッグの解除を行う
                state = FlickState.NONE;
                return;
            }

            Debug.Log("OnEndDrag eventData:" + eventData);
        }


        private float reactUnitSize;// 反応サイズ、これを超えたら要素をmoveUnitSizeまで移動させる。
        private float moveUnitSize;// 反応後移動サイズ
        // TODO: これを自動算出できるはず、next系がセットしてあれば、、、


        // ここに要素がセットしてあればその方向に動作する、これは右フリックで出てくる要素に対応
        public Corner NextLeft;
        public Corner NextRight;
        public Corner NextUp;
        public Corner NextDown;


        // フリック機能の状態
        private void WillBeginFlick(string targets)
        {

        }

        private void DidEndFlicked(string targets)
        {

        }
    }
}