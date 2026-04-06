using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Data;
using QMSFlowDoc.Shared.Models;
using WpfMessageBox = System.Windows.MessageBox;

namespace QMSFlowDoc.ServerConfigurator;

public partial class MainWindow : Window
{
    private ServerSettings _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        _settings = ServerSettingsService.Load();
        TxtSqlInstance.Text = _settings.SqlServerInstance;
        TxtDatabaseName.Text = _settings.DatabaseName;
        ChkIntegratedSecurity.IsChecked = _settings.UseIntegratedSecurity;
        TxtSqlUser.Text = _settings.SqlUsername ?? string.Empty;
        TxtSqlPassword.Password = _settings.SqlPassword ?? string.Empty;
        TxtDocPath.Text = _settings.DocumentRepositoryPath;
        TxtBackupPath.Text = _settings.BackupPath;
        ChkBackupEnabled.IsChecked = _settings.BackupEnabled;
        TxtBackupTime1.Text = _settings.BackupTime1;
        ChkBackupTime2.IsChecked = _settings.BackupTime2Enabled;
        TxtBackupTime2.Text = _settings.BackupTime2;
        TxtRetentionDays.Text = _settings.BackupRetentionDays.ToString();
        ChkFullDocBackup.IsChecked = _settings.BackupDocumentsFull;
    }

    private void SaveToModel()
    {
        _settings.SqlServerInstance = TxtSqlInstance.Text.Trim();
        _settings.DatabaseName = TxtDatabaseName.Text.Trim();
        _settings.UseIntegratedSecurity = ChkIntegratedSecurity.IsChecked == true;
        _settings.SqlUsername = TxtSqlUser.Text.Trim();
        _settings.SqlPassword = TxtSqlPassword.Password;
        _settings.DocumentRepositoryPath = TxtDocPath.Text.Trim();
        _settings.BackupPath = TxtBackupPath.Text.Trim();
        _settings.BackupEnabled = ChkBackupEnabled.IsChecked == true;
        _settings.BackupTime1 = TxtBackupTime1.Text.Trim();
        _settings.BackupTime2Enabled = ChkBackupTime2.IsChecked == true;
        _settings.BackupTime2 = TxtBackupTime2.Text.Trim();
        if (int.TryParse(TxtRetentionDays.Text, out var days) && days > 0)
            _settings.BackupRetentionDays = days;
        _settings.BackupDocumentsFull = ChkFullDocBackup.IsChecked == true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SaveToModel();
            ServerSettingsService.Save(_settings);
            WpfMessageBox.Show("Configuración guardada correctamente.",
                "QMSFlowDoc", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Error al guardar: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        SaveToModel();
        TxtSqlStatus.Text = "Probando conexión...";
        TxtSqlStatus.Foreground = System.Windows.Media.Brushes.Yellow;

        try
        {
            var connStr = _settings.BuildConnectionString();
            using var connection = new SqlConnection(connStr);
            await connection.OpenAsync();
            TxtSqlStatus.Text = $"✅ Conexión exitosa a {_settings.SqlServerInstance} / {_settings.DatabaseName}";
            TxtSqlStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtSqlStatus.Text = $"❌ Error: {ex.Message}";
            TxtSqlStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
    }

    private async void BtnCreateDatabase_Click(object sender, RoutedEventArgs e)
    {
        SaveToModel();
        TxtSqlStatus.Text = "Creando base de datos...";

        try
        {
            // Connect to master to create DB
            var masterConn = _settings.BuildConnectionString()
                .Replace($"Database={_settings.DatabaseName}", "Database=master");

            using var connection = new SqlConnection(masterConn);
            await connection.OpenAsync();

            var checkSql = $"SELECT DB_ID('{_settings.DatabaseName}')";
            using var checkCmd = new SqlCommand(checkSql, connection);
            var result = await checkCmd.ExecuteScalarAsync();

            if (result != null && result != DBNull.Value)
            {
                TxtSqlStatus.Text = $"ℹ️ La base de datos '{_settings.DatabaseName}' ya existe.";
                TxtSqlStatus.Foreground = System.Windows.Media.Brushes.Yellow;
                return;
            }

            var createSql = $"CREATE DATABASE [{_settings.DatabaseName}]";
            using var createCmd = new SqlCommand(createSql, connection);
            await createCmd.ExecuteNonQueryAsync();

            TxtSqlStatus.Text = $"✅ Base de datos '{_settings.DatabaseName}' creada correctamente.";
            TxtSqlStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtSqlStatus.Text = $"❌ Error: {ex.Message}";
            TxtSqlStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
    }

    private async void BtnApplyMigrations_Click(object sender, RoutedEventArgs e)
    {
        SaveToModel();
        TxtSqlStatus.Text = "Aplicando migraciones EF Core...";

        try
        {
            var connStr = _settings.BuildConnectionString();
            var options = new DbContextOptionsBuilder<QmsFlowDocDbContext>()
                .UseSqlServer(connStr)
                .Options;

            using var ctx = new QmsFlowDocDbContext(options);
            await ctx.Database.MigrateAsync();

            TxtSqlStatus.Text = "✅ Migraciones aplicadas correctamente. Esquema actualizado.";
            TxtSqlStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtSqlStatus.Text = $"❌ Error al migrar: {ex.Message}";
            TxtSqlStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
    }

    private void BtnBrowseDocPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Seleccione la carpeta raíz del repositorio documental",
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtDocPath.Text = dialog.SelectedPath;
    }

    private void BtnBrowseBackupPath_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Seleccione la carpeta de backups",
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            TxtBackupPath.Text = dialog.SelectedPath;
    }

    private void BtnCreateFolders_Click(object sender, RoutedEventArgs e)
    {
        var path = TxtDocPath.Text.Trim();
        if (string.IsNullOrEmpty(path))
        {
            TxtFolderStatus.Text = "❌ Especifique una ruta.";
            TxtFolderStatus.Foreground = System.Windows.Media.Brushes.Salmon;
            return;
        }

        try
        {
            var folders = new[]
            {
                "Documentos", "Documentos\\Borradores", "Documentos\\Aprobados",
                "Documentos\\Obsoletos", "Documentos\\Versiones",
                "Adjuntos", "Informes", "Certificados", "Manuales", "Logs", "Temp"
            };

            foreach (var folder in folders)
                Directory.CreateDirectory(Path.Combine(path, folder));

            TxtFolderStatus.Text = $"✅ Estructura de carpetas creada en: {path}";
            TxtFolderStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtFolderStatus.Text = $"❌ Error: {ex.Message}";
            TxtFolderStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
    }

    private void BtnTestPermissions_Click(object sender, RoutedEventArgs e)
    {
        var paths = new[] { TxtDocPath.Text.Trim(), TxtBackupPath.Text.Trim() };
        var results = new System.Text.StringBuilder();

        foreach (var path in paths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            try
            {
                if (!Directory.Exists(path))
                {
                    results.AppendLine($"❌ No existe: {path}");
                    continue;
                }
                var testFile = Path.Combine(path, $"_perm_test_{Guid.NewGuid():N}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                results.AppendLine($"✅ Escritura OK: {path}");
            }
            catch (Exception ex)
            {
                results.AppendLine($"❌ Sin permisos en {path}: {ex.Message}");
            }
        }

        TxtFolderStatus.Text = results.ToString();
        TxtFolderStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
    }

    private async void BtnBackupNow_Click(object sender, RoutedEventArgs e)
    {
        SaveToModel();
        BtnBackupNow.IsEnabled = false;
        TxtBackupStatus.Text = "⏳ Ejecutando backup manual...";
        TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Yellow;

        try
        {
            // SQL backup
            var connStr = _settings.BuildConnectionString();
            var dbBackupDir = Path.Combine(_settings.BackupPath, "DB");
            Directory.CreateDirectory(dbBackupDir);

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            var bakPath = Path.Combine(dbBackupDir, $"{_settings.DatabaseName}_{timestamp}.bak");

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();
            var sql = $"BACKUP DATABASE [{_settings.DatabaseName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION";
            using var cmd = new SqlCommand(sql, conn);
            cmd.CommandTimeout = 600;
            cmd.Parameters.AddWithValue("@path", bakPath);
            await cmd.ExecuteNonQueryAsync();

            TxtBackupStatus.Text = $"✅ Backup completado: {bakPath}";
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtBackupStatus.Text = $"❌ Error: {ex.Message}";
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
        finally
        {
            BtnBackupNow.IsEnabled = true;
        }
    }

    private void BtnViewLogs_Click(object sender, RoutedEventArgs e)
    {
        var logPath = Path.Combine(_settings.BackupPath, "Logs");
        if (Directory.Exists(logPath))
            Process.Start("explorer.exe", logPath);
        else
            WpfMessageBox.Show("La carpeta de logs no existe todavía.", "Info");
    }
}