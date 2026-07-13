using UnityEngine;
using System.Collections.Generic;

public class FountainController : MonoBehaviour
{
    [Header("Настройки префаба")]
    public GameObject fountainPrefab; 
    public List<Transform> nozzlePoints; 

    private List<GameObject> _spawnedFountains = new List<GameObject>();
    private bool _isPlaying = false;

    private void Awake()
    {
        if (fountainPrefab == null) return;

        // На старте спавним конусы в корень сцены (без родителя - передаем null),
        // чтобы они НЕ наследовали Scale от сложной иерархии штанг.
        foreach (Transform point in nozzlePoints)
        {
            if (point == null) continue;

            GameObject f = Instantiate(fountainPrefab, null);
            f.SetActive(false); // Сразу выключаем
            _spawnedFountains.Add(f);
        }
    }

    public void PlayFountains()
    {
        _isPlaying = true;
        foreach (GameObject f in _spawnedFountains)
        {
            if (f != null) f.SetActive(true);
        }
    }

    public void StopFountains()
    {
        _isPlaying = false;
        foreach (GameObject f in _spawnedFountains)
        {
            if (f != null) f.SetActive(false);
        }
    }

    // Используем LateUpdate, чтобы обновлять позицию ПОСЛЕ того, 
    // как отработали все скрипты движения штанг (избегаем отставания на 1 кадр)
    private void LateUpdate()
    {
        if (!_isPlaying) return;

        for (int i = 0; i < nozzlePoints.Count; i++)
        {
            // Проверяем, живы ли еще форсунка и сам сгенерированный конус
            if (nozzlePoints[i] != null && _spawnedFountains[i] != null)
            {
                // SetPositionAndRotation работает чуть быстрее, 
                // чем присваивание позиции и поворота по отдельности
                _spawnedFountains[i].transform.SetPositionAndRotation(
                    nozzlePoints[i].position, 
                    nozzlePoints[i].rotation
                );
            }
        }
    }

    // Так как конусы теперь лежат в корне сцены, 
    // нам нужно удалить их вручную, если этот объект уничтожается
    private void OnDestroy()
    {
        foreach (GameObject f in _spawnedFountains)
        {
            if (f != null)
            {
                Destroy(f);
            }
        }
    }
}