using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class GamenChangerServer
{
    static GamenChangerServer()
    {
        // TODO: サーバを起動して実機からの入力を受ける。実際にはWebServerで実機からのPOSTを受けるのでもいいかもしれない。
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
}
