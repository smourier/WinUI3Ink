# WinUI3Ink
A sample implementation of an InkCanvas custom control in C# for WinUI3, as only one file [InkCanvas.cs](WinUI3Ink/Controls/InkCanvas.cs) (and it's associated style in [Generic.xaml](WinUI3Ink/Themes/Generic.xaml)).

It's inspired by the "real" InkCanvas code available here https://github.com/microsoft/microsoft-ui-xaml/blob/main/src/controls/dev/InkCanvas/InkCanvas.cpp but still in experimental channel.

It's only been tested on WinAppSDK 2.0+ but already allows nice drawings 😅

<img width="1044" height="611" alt="WinUI3 InkCanvas" src="https://github.com/user-attachments/assets/9d3e0dca-4399-4612-83bd-406d06a1ec2b" />

PS: This sample's using [DirectNAot](https://github.com/smourier/DirectNAot) for all interop definitions.
