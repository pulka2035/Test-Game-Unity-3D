using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Настройки монеты")]
    [SerializeField] private int coinValue = 1;
    [SerializeField] private float rotationSpeed = 100f;
    [SerializeField] private float floatAmplitude = 0.5f;
    [SerializeField] private float floatFrequency = 1f;

    [Header("Ссылка на игрока")]
    [Tooltip("Если не назначено, будет искать автоматически")]
    [SerializeField] private Transform TransformPlayer; // Изменил имя чтобы совпадало с ошибкой

    private Vector3 startPosition;
    private float originalY;

    void Awake()
    {
        // Сохраняем стартовую позицию для парящей анимации
        startPosition = transform.position;
        originalY = transform.position.y;
    }

    void Start()
    {
        FindPlayer();
    }

    void Update()
    {
        // Анимация вращения
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);

        // Анимация парения
        float newY = originalY + Mathf.Sin(Time.time * floatFrequency) * floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // ПРОВЕРКА НА NULL ПЕРЕД ИСПОЛЬЗОВАНИЕМ!
        if (TransformPlayer != null)
        {
            // Проверка сбора монеты
            float distance = Vector3.Distance(transform.position, TransformPlayer.position);
            if (distance < 1f) // Дистанция сбора
            {
                CollectCoin();
            }
        }
        else
        {
            // Если игрок не найден, попробуем найти снова (на случай если он появился позже)
            FindPlayer();
        }
    }

    void FindPlayer()
    {
        // Если ссылка не назначена в инспекторе, ищем автоматически
        if (TransformPlayer == null)
        {
            // Сначала пробуем через синглтон
            if (ThirdPersonController.Instance != null)
            {
                TransformPlayer = ThirdPersonController.Instance.transform;
                Debug.Log($"Игрок найден через ThirdPersonController.Instance: {TransformPlayer.name}");
                return;
            }

            // Если нет синглтона, ищем по тегу
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                TransformPlayer = playerObject.transform;
                Debug.Log($"Игрок найден по тегу: {TransformPlayer.name}");
                return;
            }

            // Если не нашли по тегу, ищем по имени
            playerObject = GameObject.Find("Player");
            if (playerObject != null)
            {
                TransformPlayer = playerObject.transform;
                Debug.Log($"Игрок найден по имени: {TransformPlayer.name}");
                return;
            }

            // Если всё ещё не нашли, выводим предупреждение (не ошибку!)
            // Debug.LogWarning("Игрок не найден! Монета не будет проверять дистанцию для сбора.");
        }
    }

    void CollectCoin()
    {
        // Получаем скрипт игрока
        ThirdPersonController playerController = null;
        if (TransformPlayer != null)
        {
            playerController = TransformPlayer.GetComponent<ThirdPersonController>();
        }

        if (playerController != null)
        {
            // Добавляем монеты через метод AddCoins
            if (ThirdPersonController.Instance != null)
            {
                // Проверяем есть ли метод AddCoins в ThirdPersonController
                var method = ThirdPersonController.Instance.GetType().GetMethod("AddCoins");
                if (method != null)
                {
                    method.Invoke(ThirdPersonController.Instance, new object[] { coinValue });
                }
                else
                {
                    // Альтернатива: прямое увеличение если есть поле BitCoins
                    var field = ThirdPersonController.Instance.GetType().GetField("BitCoins");
                    if (field != null)
                    {
                        int currentCoins = (int)field.GetValue(ThirdPersonController.Instance);
                        field.SetValue(ThirdPersonController.Instance, currentCoins + coinValue);
                    }
                }
            }

            Debug.Log($"Монета собрана! Значение: {coinValue}");
        }
        else
        {
            Debug.Log($"Монета собрана, но не удалось найти ThirdPersonController!");
        }

        // Уничтожаем монету
        Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        // Альтернативный способ сбора через триггер (лучше!)
        if (other.CompareTag("Player"))
        {
            // Устанавливаем ссылку на игрока если её ещё нет
            if (TransformPlayer == null)
            {
                TransformPlayer = other.transform;
            }

            CollectCoin();
        }
    }

    // Метод для ручной установки игрока (может быть полезен)
    public void SetPlayer(Transform player)
    {
        TransformPlayer = player;
    }
}