using System;

public class MyRandom
{
    private static MyRandom instance = null;
    private static System.Random r = null;

    private MyRandom()
    {
        r = new System.Random();
    }

    public static MyRandom getInstance()
    {
        if (instance == null)
        {
            instance = new MyRandom();
        }
        return instance;
    }

    public double nextNormal()
    {
        //Box-Muller transform
        return Math.Sqrt(-2.0 * Math.Log(r.NextDouble() + double.Epsilon)) * Math.Sin(2.0 * Math.PI * r.NextDouble());
    }

    public double nextUniform()
    {
        return r.NextDouble();
    }

    public static float nrand()
    {
        return (float)MyRandom.getInstance().nextNormal();
    }

    public static float rand()
    {
        return (float)MyRandom.getInstance().nextUniform();
    }
}
