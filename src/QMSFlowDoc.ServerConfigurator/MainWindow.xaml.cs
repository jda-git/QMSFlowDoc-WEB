using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
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
        TxtMinCopies.Text = _settings.BackupMinimumCopies.ToString();
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
        if (int.TryParse(TxtMinCopies.Text, out var minCopies) && minCopies >= 1)
            _settings.BackupMinimumCopies = minCopies;
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
        TxtBackupStatus.Text = "⏳ Ejecutando backup manual (DB + Documentos)...";
        TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Yellow;

        try
        {
            // 1. SQL backup
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

            var statusParts = new System.Text.StringBuilder();
            statusParts.AppendLine($"✅ DB Backup: {bakPath}");

            // 2. Verify with RESTORE VERIFYONLY
            try
            {
                TxtBackupStatus.Text = "⏳ Verificando integridad del backup (RESTORE VERIFYONLY)...";
                var verifySql = "RESTORE VERIFYONLY FROM DISK = @path";
                using var verifyCmd = new SqlCommand(verifySql, conn);
                verifyCmd.CommandTimeout = 600;
                verifyCmd.Parameters.AddWithValue("@path", bakPath);
                await verifyCmd.ExecuteNonQueryAsync();
                statusParts.AppendLine("✅ Verificación RESTORE VERIFYONLY: PASSED");
            }
            catch (Exception vex)
            {
                statusParts.AppendLine($"⚠️ Verificación VERIFYONLY falló: {vex.Message}");
            }

            // 3. Document files backup
            if (!string.IsNullOrWhiteSpace(_settings.DocumentRepositoryPath) && Directory.Exists(_settings.DocumentRepositoryPath))
            {
                TxtBackupStatus.Text = "⏳ Copiando repositorio documental...";
                var docBackupDir = Path.Combine(_settings.BackupPath, "Files", $"Docs_{timestamp}");
                await Task.Run(() => CopyDirectoryRecursive(_settings.DocumentRepositoryPath, docBackupDir));
                statusParts.AppendLine($"✅ Docs Backup: {docBackupDir}");
            }
            else
            {
                statusParts.AppendLine("ℹ️ Repositorio documental no configurado, omitido.");
            }

            TxtBackupStatus.Text = statusParts.ToString();
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

    private static void CopyDirectoryRecursive(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite: true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectoryRecursive(dir, destSubDir);
        }
    }

    private void BtnVerifyBackup_Click(object sender, RoutedEventArgs e)
    {
        SaveToModel();
        var statusPath = Path.Combine(_settings.BackupPath, "last_backup_status.json");

        if (!File.Exists(statusPath))
        {
            TxtBackupStatus.Text = "ℹ️ No se encontró información del último backup.\nEjecute un backup primero.";
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Yellow;
            return;
        }

        try
        {
            var json = File.ReadAllText(statusPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("📋 Estado del Último Backup:");

            if (root.TryGetProperty("LastRun", out var lastRun))
                sb.AppendLine($"  Fecha: {lastRun.GetDateTime():yyyy-MM-dd HH:mm}");

            if (root.TryGetProperty("Database", out var db))
            {
                var status = db.GetProperty("Status").GetString();
                var verified = db.GetProperty("VerifyOnlyPassed").GetBoolean();
                var sizeMb = db.GetProperty("SizeBytes").GetInt64() / (1024.0 * 1024.0);
                sb.AppendLine($"  DB: {status} | Verificado: {(verified ? "✅ Sí" : "❌ No")} | Tamaño: {sizeMb:F1} MB");
            }

            if (root.TryGetProperty("Files", out var files) && files.ValueKind != JsonValueKind.Null)
            {
                var status = files.GetProperty("Status").GetString();
                var sizeMb = files.GetProperty("SizeBytes").GetInt64() / (1024.0 * 1024.0);
                sb.AppendLine($"  Archivos: {status} | Tamaño: {sizeMb:F1} MB");
            }

            TxtBackupStatus.Text = sb.ToString();
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtBackupStatus.Text = $"❌ Error al leer estado: {ex.Message}";
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
    }

    private async void BtnRestoreDb_Click(object sender, RoutedEventArgs e)
    {
        SaveToModel();

        // Step 1: Warning dialog
        var warning = WpfMessageBox.Show(
            "⚠️ ADVERTENCIA IMPORTANTE ⚠️\n\n" +
            "Esta operación reemplazará TODA la base de datos actual con un backup.\n" +
            "Todos los datos actuales se PERDERÁN irreversiblemente.\n\n" +
            "¿Desea continuar?",
            "Restauración de Base de Datos",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (warning != MessageBoxResult.Yes) return;

        // Step 2: File dialog to select .bak
        var openFile = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Seleccionar archivo de backup (.bak)",
            Filter = "SQL Server Backup (*.bak)|*.bak",
            InitialDirectory = Path.Combine(_settings.BackupPath, "DB")
        };

        if (openFile.ShowDialog() != true) return;

        // Step 3: Double confirmation - must type CONFIRMAR
        var confirmWindow = new Window
        {
            Title = "Confirmación de Restauración",
            Width = 500, Height = 250,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF1E1E2E"))
        };

        var sp = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"⚠️ Para confirmar la restauración de:\n{openFile.FileName}\n\nEscriba CONFIRMAR en el campo de texto:",
            Foreground = System.Windows.Media.Brushes.Salmon,
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12)
        });
        var confirmBox = new System.Windows.Controls.TextBox
        {
            FontSize = 16, Padding = new Thickness(8, 4, 8, 4),
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF313244")),
            Foreground = System.Windows.Media.Brushes.White
        };
        sp.Children.Add(confirmBox);
        var confirmBtn = new System.Windows.Controls.Button
        {
            Content = "Restaurar",
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(20, 8, 20, 8),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEF4444")),
            Foreground = System.Windows.Media.Brushes.White,
            FontWeight = FontWeights.Bold
        };
        confirmBtn.Click += (_, _) => { confirmWindow.DialogResult = true; confirmWindow.Close(); };
        sp.Children.Add(confirmBtn);
        confirmWindow.Content = sp;

        if (confirmWindow.ShowDialog() != true || confirmBox.Text.Trim() != "CONFIRMAR")
        {
            WpfMessageBox.Show("Restauración cancelada. No se escribió CONFIRMAR.",
                "Cancelado", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Step 4: Perform restore
        BtnRestoreDb.IsEnabled = false;
        TxtBackupStatus.Text = "⏳ Restaurando base de datos... No cierre esta ventana.";
        TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Yellow;

        try
        {
            var connStr = _settings.BuildConnectionString();
            var masterConn = connStr.Replace($"Database={_settings.DatabaseName}", "Database=master");

            using var conn = new SqlConnection(masterConn);
            await conn.OpenAsync();

            // Set single-user
            try
            {
                var singleUser = $"ALTER DATABASE [{_settings.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE";
                using var cmd1 = new SqlCommand(singleUser, conn) { CommandTimeout = 60 };
                await cmd1.ExecuteNonQueryAsync();
            }
            catch { /* DB may not exist */ }

            // Restore
            var restoreSql = $"RESTORE DATABASE [{_settings.DatabaseName}] FROM DISK = @path WITH REPLACE, RECOVERY";
            using var cmd2 = new SqlCommand(restoreSql, conn) { CommandTimeout = 1800 };
            cmd2.Parameters.AddWithValue("@path", openFile.FileName);
            await cmd2.ExecuteNonQueryAsync();

            // Multi-user
            var multiUser = $"ALTER DATABASE [{_settings.DatabaseName}] SET MULTI_USER";
            using var cmd3 = new SqlCommand(multiUser, conn) { CommandTimeout = 60 };
            await cmd3.ExecuteNonQueryAsync();

            TxtBackupStatus.Text = $"✅ Base de datos restaurada exitosamente desde:\n{openFile.FileName}";
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
        }
        catch (Exception ex)
        {
            TxtBackupStatus.Text = $"❌ Error en restauración: {ex.Message}";
            TxtBackupStatus.Foreground = System.Windows.Media.Brushes.Salmon;
        }
        finally
        {
            BtnRestoreDb.IsEnabled = true;
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