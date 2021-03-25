namespace GamenChangerCore
{
    public class GamenDriver
    {
        private readonly Corner[] steps;

        public GamenDriver(Corner[] steps)
        {
            this.steps = steps;
        }

        public void Stop()
        {
            // TODO: 何とかする。
        }

        public bool MoveNext()
        {
            // TODO: 次の〜に向けて動く、みたいなのを連続的に行う。これFlickのふりをするといいのかなーと思うが、中断とかもできないといけないんだな。
            // やっぱダイレクトに画面に対してフリックアクション発生させたい気持ちになるが、それはなーちょっとなー。
            return true;
        }
    }
}