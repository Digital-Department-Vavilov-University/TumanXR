using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq; // Добавлено для удобного поиска

namespace Enviro
{
    // Перечисление для выбора языков в инспекторе.
    // Вы можете добавлять сюда новые языки, например: German, French и т.д.
    public enum Language
    {
        English,
        Russian
    }

    // Класс для хранения всех переводимых строк для одного языка.
    // Атрибут [System.Serializable] позволяет нам видеть и редактировать его в инспекторе.
    [System.Serializable]
    public class UILanguagePack
    {
        public Language language;
        [Header("Текстовые метки")]
        public string currentWeatherPrefix = "Current Weather: ";
        public string temperaturePrefix = "Temperature: ";
        public string wetnessPrefix = "Wetness: ";
        public string snowPrefix = "Snow: ";
        public string currentSeasonPrefix = "Current Season: ";
        public string currentQualityPrefix = "Current Quality: ";

        [Header("Времена года")]
        public string seasonSpring = "Spring";
        public string seasonSummer = "Summer";
        public string seasonAutumn = "Autumn";
        public string seasonWinter = "Winter";

        public static string TranslateWeatherName(string name)
        {
            if (name == "Clear Sky") return "Чистое небо";
            if (name == "Cloudy 1") return "Облачно 1";
            if (name == "Cloudy 2") return "Облачно 2";
            if (name == "Foggy") return "Туманно";
            if (name == "Rain") return "Дождь";
            if (name == "Snow") return "Снег";
            if (name == "Cloudy 3") return "Облачно 3";

            return "Неизвестно";
        }

        public static string TranslateQualityPreset(string name)
        {
            if (name == "Low") return "Низко";
            if (name == "Medium") return "Средне";
            if (name == "High") return "Высоко";
            if (name == "Ultra") return "Ультра";
            if (name == "Insane") return "Невероятно";

            return "Неизвестно";
        }
    }

    public class UISample : MonoBehaviour
    {
        [Header("UI Elements")]
        [Header("Time")]
        public Slider hourSlider;
        public Text hourText;
        public Text dateText;
        [Header("Weather")]
        public Text currentWeatherText;
        [Header("Environment")]
        public Text seasonText;
        public Text temperatureText;
        public Text wetnessText;
        public Text snowText;
        [Header("Quality")]
        public Text currentQualityText;

        [Header("Misc")]
        public Toggle timeProgressToggle;
        public Dropdown weatherDropdown;

        [Header("Настройки Локализации")]
        // Выпадающий список для выбора текущего языка в инспекторе
        public Language currentLanguage = Language.English;
        // Список, где вы будете хранить все переводы
        public List<UILanguagePack> languagePacks = new List<UILanguagePack>();

        // Приватная переменная для хранения активного языкового пакета
        private UILanguagePack activeLangPack;
        private Language lastCheckedLanguage; // Для проверки смены языка в рантайме

        public void ResetUI()
        {
            timeProgressToggle.SetIsOnWithoutNotify(false);
            hourSlider.SetValueWithoutNotify(8f / 24f);
            weatherDropdown.SetValueWithoutNotify(0);
        }

        void Start()
        {   
            // Устанавливаем язык при старте
            SetLanguage(currentLanguage);
            lastCheckedLanguage = currentLanguage;
        }

        void Update()
        {
            // Эта проверка позволяет менять язык прямо во время игры через инспектор
            if (lastCheckedLanguage != currentLanguage)
            {
                SetLanguage(currentLanguage);
                lastCheckedLanguage = currentLanguage;
            }
        }

        /// <summary>
        /// Находит и устанавливает языковой пакет по выбранному языку.
        /// </summary>
        void SetLanguage(Language lang)
        {
            // Ищем в списке нужный языковой пакет
            activeLangPack = languagePacks.FirstOrDefault(pack => pack.language == lang);

            if (activeLangPack == null)
            {
                Debug.LogError("Языковой пакет для '" + lang.ToString() + "' не найден! Пожалуйста, добавьте и настройте его в инспекторе.");
                // В качестве запасного варианта можно использовать первый доступный пакет
                if (languagePacks.Count > 0)
                {
                    activeLangPack = languagePacks[0];
                }
            }
        }

        void LateUpdate()
        {
            // Если языковой пакет не настроен, выходим, чтобы избежать ошибок
            if (activeLangPack == null)
                return;

            if (EnviroManager.instance.Time != null)
            {
                //hourSlider.value = EnviroManager.instance.Time.GetTimeOfDay() / 24f;
                hourText.text = EnviroManager.instance.Time.GetTimeStringWithSeconds();
                dateText.text = string.Format("{0:00}/{1:00}/{2:0000}", EnviroManager.instance.Time.days, EnviroManager.instance.Time.months, EnviroManager.instance.Time.years);
            }

            if (EnviroManager.instance.Weather != null)
            {
                // Используем текст из нашего языкового пакета
                currentWeatherText.text = activeLangPack.currentWeatherPrefix + UILanguagePack.TranslateWeatherName(EnviroManager.instance.Weather.targetWeatherType.name);
            }

            if (EnviroManager.instance.Environment != null)
            {
                // Используем текст из нашего языкового пакета
                temperatureText.text = activeLangPack.temperaturePrefix + string.Format("{0:0.0} °C", EnviroManager.instance.Environment.Settings.temperature);
                wetnessText.text = activeLangPack.wetnessPrefix + string.Format("{0:0.00}", EnviroManager.instance.Environment.Settings.wetness);
                snowText.text = activeLangPack.snowPrefix + string.Format("{0:0.00}", EnviroManager.instance.Environment.Settings.snow);

                string seasonName = "";

                switch (EnviroManager.instance.Environment.Settings.season)
                {
                    case EnviroEnvironment.Seasons.Spring:
                        seasonName = activeLangPack.seasonSpring;
                        break;
                    case EnviroEnvironment.Seasons.Summer:
                        seasonName = activeLangPack.seasonSummer;
                        break;
                    case EnviroEnvironment.Seasons.Autumn:
                        seasonName = activeLangPack.seasonAutumn;
                        break;
                    case EnviroEnvironment.Seasons.Winter:
                        seasonName = activeLangPack.seasonWinter;
                        break;
                }
                // Собираем итоговую строку из префикса и названия времени года
                seasonText.text = activeLangPack.currentSeasonPrefix + seasonName;
            }

            if (EnviroManager.instance.Quality != null)
            {
                // Используем текст из нашего языкового пакета
                currentQualityText.text = activeLangPack.currentQualityPrefix + UILanguagePack.TranslateQualityPreset(EnviroManager.instance.Quality.Settings.defaultQuality.name);
            }
        }

        // Остальные методы остаются без изменений
        public void ChangeHourSlider()
        {
            if (EnviroManager.instance.Time == null)
                return;

            if (hourSlider.value < 0f)
                hourSlider.value = 0f;

            EnviroManager.instance.Time.SetTimeOfDay(hourSlider.value * 24f);
        }

        public void ChangeQuality(int q)
        {
            if (EnviroManager.instance.Quality != null)
            {
                if (EnviroManager.instance.Quality.Settings.Qualities.Count >= q)
                    EnviroManager.instance.Quality.Settings.defaultQuality = EnviroManager.instance.Quality.Settings.Qualities[q];
            }
        }

        public void ChangeWeather(int w)
        {
            if (EnviroManager.instance.Weather != null)
            {
                if (EnviroManager.instance.Weather.Settings.weatherTypes.Count >= w)
                    EnviroManager.instance.Weather.ChangeWeather(EnviroManager.instance.Weather.Settings.weatherTypes[w]);
            }
        }

        public void ChangeTimeSimulation(bool t)
        {
            if (EnviroManager.instance.Time != null)
            {
                EnviroManager.instance.Time.Settings.simulate = t;
            }
        }
    }
}