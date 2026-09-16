using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;

namespace Studio.Desktop;
internal static class Ui
{
    public static Brush Brush(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    public static readonly Brush Muted=Brush("#9CBBD1"),Green=Brush("#3ED78B"),Amber=Brush("#F4BD52"),Blue=Brush("#1599EE");
    public static TextBlock Text(string text,double size=14,Brush? color=null,bool bold=false)=>new(){Text=text,FontSize=size,Foreground=color??Brush("#E5EFF7"),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,Margin=new Thickness(0,0,0,6),TextWrapping=TextWrapping.Wrap};
    public static StackPanel Stack(params UIElement[] elements){var s=new StackPanel();foreach(var x in elements)s.Children.Add(x);return s;}
    public static WrapPanel Row(params UIElement[] elements){var s=new WrapPanel{VerticalAlignment=VerticalAlignment.Center};foreach(var x in elements)s.Children.Add(x);return s;}
    public static Border Card(UIElement body,string? title=null){var stack=new StackPanel();if(title!=null)stack.Children.Add(Text(title,18,null,true));stack.Children.Add(body);return new Border{Background=Brush("#112333"),BorderBrush=Brush("#294357"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(8),Padding=new Thickness(18),Margin=new Thickness(0,0,12,14),Child=stack};}
    public static Button Button(string label,Action action,bool primary=false){var b=new Button{Content=label};if(primary)b.Background=Brush("#086BAA");AutomationProperties.SetName(b,label);b.Click+=(_,_)=>{try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"SMC Wireless Studio",MessageBoxButton.OK,MessageBoxImage.Information);}};return b;}
    public static Button AsyncButton(string label,Func<Task> action,bool primary=false){var b=Button(label,()=>{},primary);b.Click+=async(_,_)=>{b.IsEnabled=false;try{await action();}catch(Exception ex){App.WriteError(ex);MessageBox.Show(ex.Message,"SMC Wireless Studio");}finally{b.IsEnabled=true;}};return b;}
    public static TextBox Input(string value,string name){var t=new TextBox{Text=value,MaxLength=100};AutomationProperties.SetName(t,name);return t;}
    public static FrameworkElement Field(string label,UIElement input)=>Stack(Text(label,12,Muted),input);
    public static Image Photo(string name,double height=90)=>new(){Source=new BitmapImage(new Uri("pack://application:,,,/Assets/"+name+".png")),Height=height,Stretch=Stretch.Uniform,Margin=new Thickness(6,4,14,6),HorizontalAlignment=HorizontalAlignment.Center};
    public static Grid Columns(UIElement left,UIElement right,double ratio=2){var g=new Grid();g.ColumnDefinitions.Add(new(){Width=new GridLength(ratio,GridUnitType.Star)});g.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});Grid.SetColumn(right,1);g.Children.Add(left);g.Children.Add(right);return g;}
    public static Border Badge(string text,Brush? color=null)=>new(){Background=Brush("#20313E"),BorderBrush=color??Amber,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Padding=new Thickness(9,5,9,1),Margin=new Thickness(8,0,8,8),Child=Text(text,12,color??Amber,true)};
    public static string? Prompt(Window owner,string title,string value){var input=Input(value,title);var win=new Window{Title=title,Width=470,Height=200,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=owner};var accept=Button("Mentés",()=>{win.DialogResult=true;},true);win.Content=new Border{Padding=new Thickness(24),Child=Stack(Field(title,input),Row(accept,Button("Mégse",()=>win.DialogResult=false)))};win.Loaded+=(_,_)=>{input.Focus();input.SelectAll();};return win.ShowDialog()==true?input.Text.Trim():null;}
}
