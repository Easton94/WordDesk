using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WordDesk;

public partial class MainWindow : Window
{
    private string? currentFile;
    private bool isDirty;
    private bool isLoading;

    private readonly DispatcherTimer autoSaveTimer;

    private readonly string recoveryFolder =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WordDesk",
            "Recovery");

    public MainWindow()
    {
        InitializeComponent();

        Directory.CreateDirectory(recoveryFolder);

        LoadFonts();
        LoadFontSizes();

        autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };

        autoSaveTimer.Tick += AutoSaveTimer_Tick;
        autoSaveTimer.Start();

        UpdateTitle();
    }

    private void LoadFonts()
    {
        string[] fonts =
        {
            "Arial",
            "Calibri",
            "Cambria",
            "Courier New",
            "Georgia",
            "Times New Roman",
            "Verdana"
        };

        foreach (string font in fonts)
        {
            FontBox.Items.Add(font);
        }

        FontBox.SelectedItem = "Arial";
    }

    private void LoadFontSizes()
    {
        int[] sizes =
        {
            8, 9, 10, 11, 12, 14, 16, 18, 20,
            24, 28, 32, 36, 48, 60, 72
        };

        foreach (int size in sizes)
        {
            FontSizeBox.Items.Add(size);
        }

        FontSizeBox.SelectedItem = 12;
    }

    private void UpdateTitle()
    {
        string name =
            currentFile == null
                ? "Untitled"
                : Path.GetFileName(currentFile);

        string dirtyMark = isDirty ? " *" : "";

        Title = $"WordDesk - {name}{dirtyMark}";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsavedChanges())
            return;

        isLoading = true;

        Editor.Document.Blocks.Clear();
        Editor.Document.Blocks.Add(new Paragraph());

        isLoading = false;

        currentFile = null;
        isDirty = false;

        UpdateTitle();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsavedChanges())
            return;

        OpenFileDialog dialog = new OpenFileDialog
        {
            Title = "Open Document",
            Filter =
                "Rich Text Format (*.rtf)|*.rtf|" +
                "Text Files (*.txt)|*.txt|" +
                "All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            isLoading = true;

            Editor.Document.Blocks.Clear();

            string extension =
                Path.GetExtension(dialog.FileName)
                    .ToLowerInvariant();

            if (extension == ".rtf")
            {
                using FileStream stream =
                    new FileStream(
                        dialog.FileName,
                        FileMode.Open,
                        FileAccess.Read);

                TextRange range =
                    new TextRange(
                        Editor.Document.ContentStart,
                        Editor.Document.ContentEnd);

                range.Load(stream, DataFormats.Rtf);
            }
            else
            {
                string text =
                    File.ReadAllText(dialog.FileName);

                Editor.Document.Blocks.Add(
                    new Paragraph(
                        new Run(text)));
            }

            currentFile = dialog.FileName;
            isDirty = false;

            isLoading = false;

            UpdateTitle();
        }
        catch (Exception ex)
        {
            isLoading = false;

            MessageBox.Show(
                "WordDesk could not open this file.\n\n" +
                ex.Message,
                "Open Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveDocument();
    }

    private bool SaveDocument()
    {
        if (currentFile == null)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Save Document",
                Filter =
                    "Rich Text Format (*.rtf)|*.rtf|" +
                    "Text Files (*.txt)|*.txt",
                DefaultExt = ".rtf"
            };

            if (dialog.ShowDialog() != true)
                return false;

            currentFile = dialog.FileName;
        }

        try
        {
            string extension =
                Path.GetExtension(currentFile)
                    .ToLowerInvariant();

            if (extension == ".txt")
            {
                TextRange range =
                    new TextRange(
                        Editor.Document.ContentStart,
                        Editor.Document.ContentEnd);

                File.WriteAllText(
                    currentFile,
                    range.Text);
            }
            else
            {
                using FileStream stream =
                    new FileStream(
                        currentFile,
                        FileMode.Create,
                        FileAccess.Write);

                TextRange range =
                    new TextRange(
                        Editor.Document.ContentStart,
                        Editor.Document.ContentEnd);

                range.Save(stream, DataFormats.Rtf);
            }

            isDirty = false;
            UpdateTitle();

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "WordDesk could not save this file.\n\n" +
                ex.Message,
                "Save Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    private void Editor_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (isLoading)
            return;

        isDirty = true;
        UpdateTitle();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.CanUndo)
            Editor.Undo();
    }

    private void Redo_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.CanRedo)
            Editor.Redo();
    }

    private void Window_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.S)
            {
                SaveDocument();
                e.Handled = true;
            }
            else if (e.Key == Key.N)
            {
                New_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.O)
            {
                Open_Click(sender, e);
                e.Handled = true;
            }
        }
    }

    private bool ConfirmUnsavedChanges()
    {
        if (!isDirty)
            return true;

        MessageBoxResult result =
            MessageBox.Show(
                "This document has unsaved changes.\n\n" +
                "Do you want to save them?",
                "WordDesk",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
            return SaveDocument();

        return true;
    }

    private void AutoSaveTimer_Tick(
        object? sender,
        EventArgs e)
    {
        SaveRecoverySnapshot();
        CleanOldRecoveryFiles();
    }

    private void SaveRecoverySnapshot()
    {
        try
        {
            string fileName =
                DateTime.Now.ToString(
                    "yyyy-MM-dd_HH-mm-ss") +
                ".rtf";

            string path =
                Path.Combine(
                    recoveryFolder,
                    fileName);

            using FileStream stream =
                new FileStream(
                    path,
                    FileMode.Create,
                    FileAccess.Write);

            TextRange range =
                new TextRange(
                    Editor.Document.ContentStart,
                    Editor.Document.ContentEnd);

            range.Save(stream, DataFormats.Rtf);

            EnforceRecoveryStorageLimit();
        }
        catch
        {
            // Recovery must never crash the main application.
        }
    }

    private void CleanOldRecoveryFiles()
    {
        try
        {
            foreach (FileInfo file in
                     new DirectoryInfo(recoveryFolder)
                         .GetFiles("*.rtf")
                         .OrderBy(f => f.CreationTime))
            {
                if (file.CreationTime <
                    DateTime.Now.AddMinutes(-30))
                {
                    file.Delete();
                }
            }
        }
        catch
        {
        }
    }

    private void EnforceRecoveryStorageLimit()
    {
        try
        {
            DirectoryInfo directory =
                new DirectoryInfo(recoveryFolder);

            FileInfo[] files =
                directory
                    .GetFiles("*.rtf")
                    .OrderBy(f => f.CreationTime)
                    .ToArray();

            long totalSize =
                files.Sum(f => f.Length);

            const long maxSize =
                100L * 1024L * 1024L;

            foreach (FileInfo file in files)
            {
                if (totalSize <= maxSize)
                    break;

                totalSize -= file.Length;

                try
                {
                    file.Delete();
                }
                catch
                {
                }
            }
        }
        catch
        {
        }
    }

    private void Window_Closing(
        object? sender,
        System.ComponentModel.CancelEventArgs e)
    {
        if (!ConfirmUnsavedChanges())
        {
            e.Cancel = true;
            return;
        }

        SaveRecoverySnapshot();
        autoSaveTimer.Stop();
    }

    private void SimpleMode_Click(
        object sender,
        RoutedEventArgs e)
    {
        AdvancedToolbar.Visibility =
            Visibility.Collapsed;
    }

    private void AdvancedMode_Click(
        object sender,
        RoutedEventArgs e)
    {
        AdvancedToolbar.Visibility =
            Visibility.Visible;
    }

    private void DeveloperSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "Developer Settings\n\n" +
            "Recovery snapshots: every 30 seconds\n" +
            "Recovery retention: 30 minutes\n" +
            "Recovery storage limit: 100 MB\n\n" +
            "More developer controls will be added here.",
            "WordDesk Developer Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void FontBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (Editor == null ||
            FontBox.SelectedItem == null)
            return;

        string font =
            FontBox.SelectedItem.ToString()!;

        Editor.Selection.ApplyPropertyValue(
            TextElement.FontFamilyProperty,
            new FontFamily(font));
    }

    private void FontSizeBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (Editor == null ||
            FontSizeBox.SelectedItem == null)
            return;

        if (!double.TryParse(
                FontSizeBox.SelectedItem.ToString(),
                out double size))
            return;

        Editor.Selection.ApplyPropertyValue(
            TextElement.FontSizeProperty,
            size);
    }

    private void Bold_Click(
        object sender,
        RoutedEventArgs e)
    {
        ToggleFormatting(
            TextElement.FontWeightProperty,
            FontWeights.Bold,
            FontWeights.Normal);
    }

    private void Italic_Click(
        object sender,
        RoutedEventArgs e)
    {
        ToggleFormatting(
            TextElement.FontStyleProperty,
            FontStyles.Italic,
            FontStyles.Normal);
    }

    private void Underline_Click(
        object sender,
        RoutedEventArgs e)
    {
        object value =
            Editor.Selection.GetPropertyValue(
                Inline.TextDecorationsProperty);

        if (value == DependencyProperty.UnsetValue)
        {
            Editor.Selection.ApplyPropertyValue(
                Inline.TextDecorationsProperty,
                TextDecorations.Underline);
        }
        else
        {
            Editor.Selection.ApplyPropertyValue(
                Inline.TextDecorationsProperty,
                null);
        }
    }

    private void ToggleFormatting(
        DependencyProperty property,
        object activeValue,
        object normalValue)
    {
        object current =
            Editor.Selection.GetPropertyValue(property);

        if (current == DependencyProperty.UnsetValue ||
            !Equals(current, activeValue))
        {
            Editor.Selection.ApplyPropertyValue(
                property,
                activeValue);
        }
        else
        {
            Editor.Selection.ApplyPropertyValue(
                property,
                normalValue);
        }
    }

    private void AlignLeft_Click(
        object sender,
        RoutedEventArgs e)
    {
        Editor.Selection.ApplyPropertyValue(
            Paragraph.TextAlignmentProperty,
            TextAlignment.Left);
    }

    private void AlignCenter_Click(
        object sender,
        RoutedEventArgs e)
    {
        Editor.Selection.ApplyPropertyValue(
            Paragraph.TextAlignmentProperty,
            TextAlignment.Center);
    }

    private void AlignRight_Click(
        object sender,
        RoutedEventArgs e)
    {
        Editor.Selection.ApplyPropertyValue(
            Paragraph.TextAlignmentProperty,
            TextAlignment.Right);
    }

    private void Justify_Click(
        object sender,
        RoutedEventArgs e)
    {
        Editor.Selection.ApplyPropertyValue(
            Paragraph.TextAlignmentProperty,
            TextAlignment.Justify);
    }

    private void Print_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            PrintDialog printDialog =
                new PrintDialog();

            if (printDialog.ShowDialog() != true)
                return;

            IDocumentPaginatorSource paginator =
                Editor.Document;

            printDialog.PrintDocument(
                paginator.DocumentPaginator,
                "WordDesk Document");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Printing failed.\n\n" +
                ex.Message,
                "Print Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Pdf_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "PDF export will be added in the next WordDesk build.",
            "WordDesk",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void FindReplace_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "Find / Replace is planned for the next advanced editor update.",
            "WordDesk",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void PageSettings_Click(
        object sender,
        RoutedEventArgs e)
    {
        MessageBox.Show(
            "Page Settings will be added in the next document-layout update.",
            "WordDesk",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }
}
