using GamenChangerCore;
using UnityEngine;

public class MyCornerContents : MonoBehaviour, ICornerContent
{
    public void CornerTouchDetected()
    {
        // Debug.Log("Touch" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerWillAppear()
    {
        // Debug.Log("WillAppear" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerDidAppear()
    {
        // Debug.Log("DidAppear" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerWillDisappear()
    {
        // Debug.Log("WillDisappear" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerDidDisappear()
    {
        // Debug.Log("DidDisappear" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerAppearCancelled()
    {
        // Debug.Log("AppearCancelled" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerDisppearCancelled()
    {
        // Debug.Log("DisppearCancelled" + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerAppearProgress(float progress)
    {
        // Debug.Log("AppearProgress:" + progress + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerDisppearProgress(float progress)
    {
        // Debug.Log("DisppearProgress:" + progress + "\tparent:" + transform.parent.gameObject);
    }

    public void CornerWillCancel()
    {

    }

    public void CornerWillBack()
    {

    }
}