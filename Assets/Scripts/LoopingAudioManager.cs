using UnityEngine;
using System.Collections;

public class LoopingAudioManager : MonoBehaviour
{
    public AudioSource audioSource; // Аудиоисточник
    public float fadeDuration = 0.5f; // Длительность затухания/нарастания

    private Coroutine currentFadeCoroutine; // Ссылка на текущую корутину для предотвращения повторного вызова

    // Запускает звук с плавным нарастанием
    public void PlayWithFadeIn()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadeAudio(0f, 1f));
    }

    // Останавливает звук с плавным затуханием
    public void StopWithFadeOut()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadeAudio(1f, 0f));
    }

    // Основной метод для плавного изменения громкости
    private IEnumerator FadeAudio(float startVolume, float endVolume)
    {
        audioSource.volume = startVolume;

        if (Mathf.Approximately(startVolume, 0f))
        {
            audioSource.Play(); // Запускаем звук, если он ещё не воспроизводится
        }

        float timeElapsed = 0f;
        while (timeElapsed < fadeDuration)
        {
            audioSource.volume = Mathf.Lerp(startVolume, endVolume, timeElapsed / fadeDuration);
            timeElapsed += Time.deltaTime;
            yield return null;
        }

        audioSource.volume = endVolume;

        if (Mathf.Approximately(endVolume, 0f))
        {
            audioSource.Stop(); // Останавливаем звук, если он затих
        }

        currentFadeCoroutine = null; // Сбрасываем ссылку на корутину
    }
}
