using GamenChangerCore;
using UnityEngine;

public class FlickDetector : MonoBehaviour, IFlickableCornerHandler
{
    public GameObject FlickableCornerPrefab;

    /*
        このレイヤーで知りたいのは、
        今どのビューがfocusされてるか、他
    */
    public void WillAppear(FlickableCorner flickableCorner)
    {
        Debug.Log("WillAppear:" + flickableCorner);
    }
    public void DidAppear(FlickableCorner flickableCorner)
    {
        Debug.Log("DidAppear:" + flickableCorner);
    }
    public void AppearCancelled(FlickableCorner flickableCorner)
    {
        Debug.Log("AppearCancelled:" + flickableCorner);
    }
    public void AppearProgress(FlickableCorner flickableCorner, float progress)
    {
        Debug.Log("AppearProgress:" + flickableCorner + " progress:" + progress);
    }


    public void WillDisappear(FlickableCorner flickableCorner)
    {
        Debug.Log("WillDisappear:" + flickableCorner);
    }
    public void DidDisappear(FlickableCorner flickableCorner)
    {
        Debug.Log("DidDisappear:" + flickableCorner);
        if (flickableCorner.gameObject.name.Contains("FrickableCornerPrefab"))
        {
            // prefabから作ったやつだったら消す
            Destroy(flickableCorner.gameObject);
        }
    }
    public void DisppearCancelled(FlickableCorner flickableCorner)
    {
        Debug.Log("DisppearCancelled:" + flickableCorner);
    }
    public void DisppearProgress(FlickableCorner flickableCorner, float progress)
    {
        Debug.Log("DisppearProgress:" + flickableCorner + " progress:" + progress);
    }

    // フリックの開始時にリクエストを検知し、ビューの建て増しと削除が可能になる。
    public void OnFlickRequestFromFlickableCorner(FlickableCorner flickableCorner, ref Corner cornerFromLeft, ref Corner cornerFromRight, ref Corner cornerFromTop, ref Corner cornerFromBottom, FlickDirection plannedFlickDir)
    {
        // leftが空なFlickableCornerに対して右フリックをした際、左側にコンテンツを偽造する
        if (plannedFlickDir == FlickDirection.RIGHT && cornerFromLeft == null)
        {
            // cornerFromLeftに代入する
            var newCorner = Instantiate(FlickableCornerPrefab, this.transform).GetComponent<FlickableCorner>();

            // 左側にくるようにセット
            newCorner.currentRectTransform.anchoredPosition = new Vector2(flickableCorner.currentRectTransform.anchoredPosition.x - flickableCorner.currentRectTransform.sizeDelta.x, flickableCorner.currentRectTransform.anchoredPosition.y);

            newCorner.CornerFromRight = flickableCorner;
            cornerFromLeft = newCorner;
            return;
        }

        // rightが空なFlickableCornerに対して左フリックをした際、右側にコンテンツを偽造する
        if (plannedFlickDir == FlickDirection.LEFT && cornerFromRight == null)
        {
            // cornerFromRightに代入する
            var newCorner = Instantiate(FlickableCornerPrefab, this.transform).GetComponent<FlickableCorner>();

            // 右側にくるようにセット
            newCorner.currentRectTransform.anchoredPosition = new Vector2(flickableCorner.currentRectTransform.anchoredPosition.x + flickableCorner.currentRectTransform.sizeDelta.x, flickableCorner.currentRectTransform.anchoredPosition.y);

            newCorner.CornerFromLeft = flickableCorner;
            cornerFromRight = newCorner;
            return;
        }
    }
}
