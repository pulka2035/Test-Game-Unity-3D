using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CloudSaveTest : MonoBehaviour
{
    async void Start()
    {
        // Подключение к облаку
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        Debug.Log("✅ Подключено! ID игрока: " + AuthenticationService.Instance.PlayerId);

        // Сохраняем данные
        await SaveData();

        // Загружаем данные
        await LoadData();
    }

    async Task SaveData()
    {
        var data = new Dictionary<string, object>
        {
            { "player_coins", 1500 },
            { "player_level", 5 },
            { "player_name", "Герой" }
        };

        await CloudSaveService.Instance.Data.Player.SaveAsync(data);
        Debug.Log("✅ Данные сохранены!");
    }

    async Task LoadData()
    {
        var keys = new HashSet<string> { "player_coins", "player_level", "player_name" };
        var results = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

        if (results.TryGetValue("player_coins", out var coins))
            Debug.Log("💰 Монеты: " + coins.Value.GetAs<int>());

        if (results.TryGetValue("player_level", out var level))
            Debug.Log("⭐ Уровень: " + level.Value.GetAs<int>());

        if (results.TryGetValue("player_name", out var name))
            Debug.Log("👤 Имя: " + name.Value.GetAs<string>());
    }
}