# WinUI3Ink
A sample implementation of an **InkCanvas** custom control in C# for WinUI3, as only one file [InkCanvas.cs](WinUI3Ink/Controls/InkCanvas.cs) (and it's associated style in [Generic.xaml](WinUI3Ink/Themes/Generic.xaml)).

It's inspired by the "real" InkCanvas code available here https://github.com/microsoft/microsoft-ui-xaml/blob/main/src/controls/dev/InkCanvas/InkCanvas.cpp but still in experimental channel.

The InkCanvas also has a `GetBitmap` function that creates a `SoftwareBitmap` from the strokes (with transparent or opaque background).

It's only been tested on WinAppSDK 2.0+ but already allows nice drawings 😅

<img width="1112" height="639" alt="WinUI3 InkCanvas" src="https://github.com/user-attachments/assets/3ee348a0-e78c-4ca2-b351-0f1559d8a4ca" />

PS: This sample's using [DirectNAot](https://github.com/smourier/DirectNAot) and [WicNet](https://github.com/smourier/WicNet) for all Windows Inking, Direct Composition, Direct2D and WIC interop definitions.
