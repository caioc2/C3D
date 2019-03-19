using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class DayNightCycle : MonoBehaviour {

    public Material NightSkybox;
    public Material DaySkybox;
    public Material EveningSkybox;
    public Material DawnSkybox;
    public float speed = 0.01f;
    public float maxIntensity = 2.5f;
    public GameObject sun;
    public double latitude;
    public double longitude;

    private float time = 0;
    private float nf = 0.0f;
    private int state = 0;
    private Material cur;

    private DateTime start;
    private AudioSource nightAudio;
    private AudioSource dayAudio;

    public bool isNightTime()
    {
        return state == 20;
    }

    void Start () {
        cur = (Material)Resources.Load("SkyBlendMat", typeof(Material));
        RenderSettings.skybox = cur;
        DynamicGI.UpdateEnvironment();

        if(!sun)
        {
            sun = GameObject.Find("Sun");
        }
        start = new DateTime(2018, 5, 14, 20, 0, 0);

        nightAudio = GetComponents<AudioSource>()[0];
        dayAudio = GetComponents<AudioSource>()[1];
        toggleStop = false;
    }

    private bool toggleStop;
	void Update () {

        if(Input.GetKeyUp(KeyCode.PageUp))
        {
            toggleStop = !toggleStop;
        }

        if (toggleStop) return;

        time += speed * Time.deltaTime;

        int now = ((int)Math.Floor(time)) % 24;
        float t = time - (int)time + now;
        bool update = true;

        switch(now)
        {
            case 8:
            case 9:
            case 10:
            case 11:
            case 12:
            case 13:
                setMaterialCubeMap(cur, DaySkybox, DaySkybox, 0.0f);
                update = state == 6 ? false : true;
                state = 6;
                nf = 0.0f;
                break;
            case 14:
            case 15:
                setMaterialCubeMap(cur, DaySkybox, EveningSkybox, (t - 14.0f)/2.0f);
                update = true;
                state = 14;
                nf = 0.0f;
                break;
            case 16:
            case 17:
                setMaterialCubeMap(cur, EveningSkybox, EveningSkybox, 0.0f);
                update = state == 16 ? false : true;
                state = 16;
                nf = 0.0f;
                break;
            case 18:
            case 19:
                nf = (t - 18.0f) / 2.0f;
                setMaterialCubeMap(cur, EveningSkybox, NightSkybox, nf);
                update = true;
                state = 18;
                break;
            case 20:
            case 21:
            case 22:
            case 23:
            case 0:
            case 1:
            case 2:
            case 3:
                setMaterialCubeMap(cur, NightSkybox, NightSkybox, 0.0f);
                update = state == 20 ? false : true;
                state = 20;
                nf = 1.0f;
                break;
            case 4:
            case 5:
                setMaterialCubeMap(cur, NightSkybox, DawnSkybox, (t - 4.0f) / 2.0f);
                nf = 1.0f - (t - 4.0f) / 2.0f;
                update = true;
                state = 4;
                break;
            case 6:
            case 7:
                setMaterialCubeMap(cur, DawnSkybox, DaySkybox, (t - 6.0f) / 2.0f);
                update = true;
                state = 6;
                nf = 0.0f;
                break;
            default:
                break;
        }
        if (update)
        {
            RenderSettings.skybox = cur;
            DynamicGI.UpdateEnvironment();
        }

        DateTime n = start.AddHours(time);
        double altitude = 0, azimuth = 0;
        SunPosition.CalculateSunPosition(n, latitude, longitude, out azimuth, out altitude);
        Vector3 angles = new Vector3((float)altitude * 180.0f / (float)Math.PI, (float)azimuth * 180.0f / (float)Math.PI, 0.0f);

        sun.transform.localRotation = Quaternion.Euler(angles);
        if(angles.x < 0.0f)
        {
            sun.GetComponent<Light>().intensity = 0.0f;
        } else
        {
            sun.GetComponent<Light>().intensity = maxIntensity * (float)Math.Sin(altitude);
        }

        nightAudio.volume = nf*nf;
        dayAudio.volume = (1.0f - nf) * (1.0f - nf);
    }

    private void setMaterialCubeMap(Material toSet, Material a, Material b, float blend)
    {
        toSet.SetTexture("_FrontTex", a.GetTexture("_FrontTex"));
        toSet.SetTexture("_BackTex", a.GetTexture("_BackTex"));
        toSet.SetTexture("_LeftTex", a.GetTexture("_LeftTex"));
        toSet.SetTexture("_RightTex", a.GetTexture("_RightTex"));
        toSet.SetTexture("_UpTex", a.GetTexture("_UpTex"));
        toSet.SetTexture("_DownTex", a.GetTexture("_DownTex"));

        toSet.SetTexture("_FrontTex2", b.GetTexture("_FrontTex"));
        toSet.SetTexture("_BackTex2", b.GetTexture("_BackTex"));
        toSet.SetTexture("_LeftTex2", b.GetTexture("_LeftTex"));
        toSet.SetTexture("_RightTex2", b.GetTexture("_RightTex"));
        toSet.SetTexture("_UpTex2", b.GetTexture("_UpTex"));
        toSet.SetTexture("_DownTex2", b.GetTexture("_DownTex"));
        toSet.SetFloat("_Blend", blend);

    }

    /*
     * The following source came from this blog:
     * http://guideving.blogspot.co.uk/2010/08/sun-position-in-c.html
     */
    public static class SunPosition
    {
        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        /*! 
         * \brief Calculates the sun light. 
         * 
         * CalcSunPosition calculates the suns "position" based on a 
         * given date and time in local time, latitude and longitude 
         * expressed in decimal degrees. It is based on the method 
         * found here: 
         * http://www.astro.uio.no/~bgranslo/aares/calculate.html 
         * The calculation is only satisfiably correct for dates in 
         * the range March 1 1900 to February 28 2100. 
         * \param dateTime Time and date in local time. 
         * \param latitude Latitude expressed in decimal degrees. 
         * \param longitude Longitude expressed in decimal degrees. 
         */
        public static void CalculateSunPosition(
            DateTime dateTime, double latitude, double longitude, out double outAzimuth, out double outAltitude)
        {
            // Convert to UTC  
            dateTime = dateTime.ToUniversalTime();

            // Number of days from J2000.0.  
            double julianDate = 367 * dateTime.Year -
                (int)((7.0 / 4.0) * (dateTime.Year +
                (int)((dateTime.Month + 9.0) / 12.0))) +
                (int)((275.0 * dateTime.Month) / 9.0) +
                dateTime.Day - 730531.5;

            double julianCenturies = julianDate / 36525.0;

            // Sidereal Time  
            double siderealTimeHours = 6.6974 + 2400.0513 * julianCenturies;

            double siderealTimeUT = siderealTimeHours +
                (366.2422 / 365.2422) * (double)dateTime.TimeOfDay.TotalHours;

            double siderealTime = siderealTimeUT * 15 + longitude;

            // Refine to number of days (fractional) to specific time.  
            julianDate += (double)dateTime.TimeOfDay.TotalHours / 24.0;
            julianCenturies = julianDate / 36525.0;

            // Solar Coordinates  
            double meanLongitude = CorrectAngle(Deg2Rad *
                (280.466 + 36000.77 * julianCenturies));

            double meanAnomaly = CorrectAngle(Deg2Rad *
                (357.529 + 35999.05 * julianCenturies));

            double equationOfCenter = Deg2Rad * ((1.915 - 0.005 * julianCenturies) *
                Math.Sin(meanAnomaly) + 0.02 * Math.Sin(2 * meanAnomaly));

            double elipticalLongitude =
                CorrectAngle(meanLongitude + equationOfCenter);

            double obliquity = (23.439 - 0.013 * julianCenturies) * Deg2Rad;

            // Right Ascension  
            double rightAscension = Math.Atan2(
                Math.Cos(obliquity) * Math.Sin(elipticalLongitude),
                Math.Cos(elipticalLongitude));

            double declination = Math.Asin(
                Math.Sin(rightAscension) * Math.Sin(obliquity));

            // Horizontal Coordinates  
            double hourAngle = CorrectAngle(siderealTime * Deg2Rad) - rightAscension;

            if (hourAngle > Math.PI)
            {
                hourAngle -= 2 * Math.PI;
            }

            double altitude = Math.Asin(Math.Sin(latitude * Deg2Rad) *
                Math.Sin(declination) + Math.Cos(latitude * Deg2Rad) *
                Math.Cos(declination) * Math.Cos(hourAngle));

            // Nominator and denominator for calculating Azimuth  
            // angle. Needed to test which quadrant the angle is in.  
            double aziNom = -Math.Sin(hourAngle);
            double aziDenom =
                Math.Tan(declination) * Math.Cos(latitude * Deg2Rad) -
                Math.Sin(latitude * Deg2Rad) * Math.Cos(hourAngle);

            double azimuth = Math.Atan(aziNom / aziDenom);

            if (aziDenom < 0) // In 2nd or 3rd quadrant  
            {
                azimuth += Math.PI;
            }
            else if (aziNom < 0) // In 4th quadrant  
            {
                azimuth += 2 * Math.PI;
            }

            outAltitude = altitude;
            outAzimuth = azimuth;
        }

        /*! 
        * \brief Corrects an angle. 
        * 
        * \param angleInRadians An angle expressed in radians. 
        * \return An angle in the range 0 to 2*PI. 
        */
        private static double CorrectAngle(double angleInRadians)
        {
            if (angleInRadians < 0)
            {
                return 2 * Math.PI - (Math.Abs(angleInRadians) % (2 * Math.PI));
            }
            else if (angleInRadians > 2 * Math.PI)
            {
                return angleInRadians % (2 * Math.PI);
            }
            else
            {
                return angleInRadians;
            }
        }
    }
}
