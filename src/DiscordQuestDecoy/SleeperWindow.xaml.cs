using System.Windows;

namespace DiscordQuestDecoy;

/// <summary>The stand-in "game" window. Deliberately plain: default colours, nothing custom.</summary>
public partial class SleeperWindow : Window
{
    public SleeperWindow(string title, string text)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        BodyText.Text = text;
    }
}
