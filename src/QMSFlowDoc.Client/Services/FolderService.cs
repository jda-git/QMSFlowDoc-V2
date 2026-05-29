using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

using System.Linq; // Added for Linq extension methods

namespace QMSFlowDoc.Client.Services;

public interface IFolderService
{
    Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid? parentId = null);
    Task<IEnumerable<FolderDto>> GetAllFoldersAsync();
    Task<bool> CreateFolderAsync(string name, Guid? parentId = null);
    Task<bool> RenameFolderAsync(Guid id, string newName, Guid? parentId = null);
    Task<bool> DeleteFolderAsync(Guid id);
}

public class FolderService : IFolderService
{
    private readonly HttpClient _httpClient;
    private readonly NetworkConfigStore _networkConfig;
    private LocalDocumentStore? _localStore;

    public FolderService(HttpClient httpClient, LocalDocumentStore? localStore = null, NetworkConfigStore? networkConfig = null)
    {
        _httpClient = httpClient;
        _networkConfig = networkConfig ?? new NetworkConfigStore();
        _localStore = localStore;
    }

    private async Task<LocalDocumentStore> GetLocalStoreAsync()
    {
        if (_localStore == null)
        {
            _localStore = new LocalDocumentStore(_networkConfig);
            await _localStore.InitializeAsync();
        }
        return _localStore;
    }

    public async Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid? parentId = null)
    {
        try
        {
            var url = "folders";
            if (parentId.HasValue) url += $"?parentId={parentId.Value}";
            return await _httpClient.GetFromJsonAsync<IEnumerable<FolderDto>>(url) ?? new List<FolderDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            var allFolders = await store.GetFoldersAsync();
            return allFolders.Where(f => f.ParentFolderId == parentId).ToList();
        }
    }

    public async Task<IEnumerable<FolderDto>> GetAllFoldersAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<IEnumerable<FolderDto>>("folders/all") ?? new List<FolderDto>();
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.GetFoldersAsync();
        }
    }

    public async Task<bool> CreateFolderAsync(string name, Guid? parentId = null)
    {
        try
        {
            var response = await _httpClient.PostAsync($"folders?name={Uri.EscapeDataString(name)}&parentId={parentId}", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.CreateFolderAsync(name, parentId);
        }
    }

    public async Task<bool> RenameFolderAsync(Guid id, string newName, Guid? parentId = null)
    {
        try
        {
            var url = $"folders/{id}?name={Uri.EscapeDataString(newName)}";
            if (parentId.HasValue) url += $"&parentId={parentId.Value}";
            var response = await _httpClient.PutAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.RenameFolderAsync(id, newName, parentId);
        }
    }

    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"folders/{id}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            var store = await GetLocalStoreAsync();
            return await store.DeleteFolderAsync(id);
        }
    }
}
