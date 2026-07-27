using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;

public class CloudSaveExample : MonoBehaviour
{
    async void Start()
    {
        // 1. Инициализация сервисов Unity
        await UnityServices.InitializeAsync();

        // 2. Анонимная авторизация (обязательно! Без этого CloudSave не сработает)
        // Если авторизация уже есть, этот шаг пропустится автоматически
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Авторизация прошла успешно. Player ID: {AuthenticationService.Instance.PlayerId}");
        }

        // Сохраняем тестовые данные
        await SaveData();
    }

    // Метод для сохранения
    async System.Threading.Tasks.Task SaveData()
    {
        var data = new Dictionary<string, object>
        {
            { "MySaveKey", "Hello Cloud!" },
            { "PlayerHealth", 100 },
            { "PlayerLevel", 5 }
        };

        // Сохраняем данные в облако
        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        Debug.Log("Данные успешно сохранены в облако!");
    }
}