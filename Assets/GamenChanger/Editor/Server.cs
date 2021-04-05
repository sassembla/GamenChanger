using UnityEngine;
using UnityEditor;
using System;

[InitializeOnLoad]
public class GamenChangerServer
{
    static GamenChangerServer()
    {
        // QRコードを作り出す必要がある。これは一度全部書き出してみるか。
        // UpdateDeviceUI();


    }

    [MenuItem("Window/GamenChanger/Generate QR code")]
    public static void GenQRCode()
    {
        /*
            TODO: UIを実機側に送り出せるようにQRコード化する。
            QRコードを読んだ実機側へとUIを送り出し、UIを操作した際にその情報をエディタへと転送する。
            シーン全体をprefab化して送り込めるようにするとすげー楽な気がする。
            あくまでUIに限定しないといけないっていうのはあるなー、存在しないコードのmissingにどう対処するかとかを考えないといけない。
            
            目的は「ビルド回数を削る」ことなんだ。
            
            以下のことをビルドなしで調整可能にしたい。
            ・スワイプ反応など、アニメーション操作の調整系の値
            ・画像などデザインの調整
        */
        Debug.Log("GenQRCode");
    }

    [MenuItem("Window/GamenChanger/Update Device UI")]
    public static void UpdateDeviceUI()
    {
        // 今のEditorのUIを端末に送り出す
        Debug.Log("SendUIToDevice");

        // Play中に画像貼ったり変えたり値を調整したりして、それがそのまま実機にいく、というのを想定している。


        var sessionGameObjectName = "GamenChangerAnchor_" + Guid.NewGuid().ToString();
        var go = GameObject.Find(sessionGameObjectName);
        if (go == null)
        {
            go = new GameObject(sessionGameObjectName);
        }
        using (new EditorSession(go))
        {
            var parent = go.transform.parent;
            Debug.Log("nullなはず parent:" + parent);








        }
    }

    private class EditorSession : IDisposable
    {
        private bool disposedValue;
        private readonly GameObject go;

        public EditorSession(GameObject go)
        {
            this.go = go;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    GameObject.DestroyImmediate(go);
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
