using GamenChangerCore;
using UnityEngine;

public class FlickDetector : MonoBehaviour, IFlickableCornerFocusHandler
{
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
        // Destroy(flickableCorner.gameObject);// 消すと戻れなくなる
    }
    public void DisppearCancelled(FlickableCorner flickableCorner)
    {
        Debug.Log("DisppearCancelled:" + flickableCorner);
    }
    public void DisppearProgress(FlickableCorner flickableCorner, float progress)
    {
        Debug.Log("DisppearProgress:" + flickableCorner + " progress:" + progress);
    }


}
