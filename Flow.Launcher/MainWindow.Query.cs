using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using DataObject = System.Windows.DataObject;

namespace Flow.Launcher
{
    public partial class MainWindow
    {
        #region QueryTextBox Event

        private void QueryTextBox_OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            var result = _viewModel.Results.SelectedItem?.Result;
            if (QueryTextBox.SelectionLength == 0 && result != null)
            {
                string copyText = result.CopyText;
                App.API.CopyToClipboard(copyText, directCopy: true);
            }
            else if (!string.IsNullOrEmpty(QueryTextBox.Text))
            {
                App.API.CopyToClipboard(QueryTextBox.SelectedText, showDefaultNotification: false);
            }
        }

        private void QueryTextBox_OnPaste(object sender, DataObjectPastingEventArgs e)
        {
            try
            {
                var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
                if (isText)
                {
                    var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                    text = text.Replace(Environment.NewLine, " ");
                    DataObject data = new DataObject();
                    data.SetData(DataFormats.UnicodeText, text);
                    e.DataObject = data;
                }
            }
            catch (Exception ex)
            {
                App.API.LogException(ClassName, "Failed to paste text", ex);
            }
        }

        private void QueryTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (_viewModel.QueryText != QueryTextBox.Text)
            {
                BindingExpression be = QueryTextBox.GetBindingExpression(TextBox.TextProperty);
                be.UpdateSource();
            }
        }

        private void QueryTextBox_OnPreviewDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        #endregion

        #region Placeholder

        private void SetupPlaceholderText()
        {
            if (_settings.ShowPlaceholder)
            {
                QueryTextBox.TextChanged += QueryTextBox_TextChanged;
                QueryTextSuggestionBox.TextChanged += QueryTextSuggestionBox_TextChanged;
                SetPlaceholderText();
            }
            else
            {
                QueryTextBox.TextChanged -= QueryTextBox_TextChanged;
                QueryTextSuggestionBox.TextChanged -= QueryTextSuggestionBox_TextChanged;
                QueryTextPlaceholderBox.Visibility = Visibility.Collapsed;
            }
        }

        private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetPlaceholderText();
        }

        private void QueryTextSuggestionBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SetPlaceholderText();
        }

        private void SetPlaceholderText()
        {
            var queryText = QueryTextBox.Text;
            var suggestionText = QueryTextSuggestionBox.Text;
            QueryTextPlaceholderBox.Visibility = string.IsNullOrEmpty(queryText) && string.IsNullOrEmpty(suggestionText) ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
