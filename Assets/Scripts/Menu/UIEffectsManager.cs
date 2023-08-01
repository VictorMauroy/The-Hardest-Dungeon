using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public delegate void MaskedSceneEvent();

public class UIEffectsManager : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    private Color blackFadeInitialColor = new Color(0f, 0f, 0f, 0f);
    private Color blackFadeTargetColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private AnimationCurve fadeInCurve;
    [SerializeField] private AnimationCurve fadeOutCurve;

    public static MaskedSceneEvent OnMaskedScene;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ClassicBlackFade(float fadeTime, float inBetweenDelay)
    {
        StartCoroutine(FadeInAndOut(blackFadeInitialColor, blackFadeTargetColor, fadeTime, inBetweenDelay));
    }

    IEnumerator FadeInAndOut(Color clearColor, Color fullColor, float fadeTime, float inBetweenDelay)
    {
        fadeImage.gameObject.SetActive(true);
        fadeImage.color = clearColor;
        
        float startTime = Time.time;
        float fractionOfJourney;

        //FADE IN
        do
        {
            fractionOfJourney = (Time.time - startTime) / fadeTime;

            fadeImage.color = Color.Lerp(clearColor, fullColor, fadeInCurve.Evaluate(fractionOfJourney));

            yield return null;
        } while (Time.time < startTime + fadeTime);

        if (OnMaskedScene != null) OnMaskedScene();

        yield return new WaitForSeconds(inBetweenDelay); //Délai entre deux fondus.

        startTime = Time.time;
        
        //FADE OUT
        do
        {
            fractionOfJourney = (Time.time - startTime) / fadeTime;

            fadeImage.color = Color.Lerp(fullColor, clearColor, fadeOutCurve.Evaluate(fractionOfJourney));

            yield return null;
        } while (Time.time < startTime + fadeTime);

        fadeImage.gameObject.SetActive(false);
    }
}
