using UnityEngine;

public class Randomize : MonoBehaviour
{
    GameObject ring, asteroid;
    Vector3 corner1, corner2;
    enum Fun {Trig, Exp, Poly, Log}

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //randomize parametric function for each variable
        System.Func<float, float>[] rands = new System.Func<float, float>[3];
        for (int i = 0; i < rands.Length; i++)
        {
            Fun r = (Fun)Random.Range(0,4);
            float coef = Random.Range(-10f,10f), pow = Random.Range(-3f,3f), bas = Random.Range(0f,5f);
            switch (r)
            {
                
                case Fun.Trig:
                    rands[i] = t => {return 0;};
                    break;
                case Fun.Exp:
                    rands[i] = t => {return 0;};
                    break;
                case Fun.Poly:
                    rands[i] = t => {return coef * Mathf.Pow(t, pow);};
                    break;
                case Fun.Log:
                    rands[i] = t => {return coef * Mathf.Log(t, bas);};
                    break;
            }
            rands[i] = t => {return 0;};
        }

        for (int t = 0; t < 20; t++)
        {
            GameObject ringClone = Instantiate(ring);
            ringClone.transform.position = new Vector3(rands[0](t), rands[1](t), rands[2](t));
        }

        for (int i = 0; i < 50; i++)
        {
            
        }
    }
}