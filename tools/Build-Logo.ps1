$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework
$root=Split-Path $PSScriptRoot
[xml]$xaml=Get-Content -LiteralPath (Join-Path $root 'Assets\Logo.xaml') -Raw
$visual=[Windows.Markup.XamlReader]::Load([Xml.XmlNodeReader]::new($xaml))
$visual.Measure([Windows.Size]::new(256,256));$visual.Arrange([Windows.Rect]::new(0,0,256,256));$visual.UpdateLayout()
$frames=@()
foreach($size in @(16,24,32,48,64,128,256)){
 $bitmap=[Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
 $drawing=[Windows.Media.DrawingVisual]::new();$context=$drawing.RenderOpen()
 $context.DrawRectangle([Windows.Media.VisualBrush]::new($visual),$null,[Windows.Rect]::new(0,0,$size,$size));$context.Close()
 $bitmap.Render($drawing)
 $encoder=[Windows.Media.Imaging.PngBitmapEncoder]::new();$encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
 $stream=[IO.MemoryStream]::new();$encoder.Save($stream);$bytes=$stream.ToArray();$stream.Dispose()
 $frames+=,@{Size=$size;Bytes=$bytes}
 if($size -eq 256){[IO.File]::WriteAllBytes((Join-Path $root 'Assets\Logo.png'),$bytes)}
}
$file=[IO.File]::Create((Join-Path $root 'Assets\WinProvision.ico'));$writer=[IO.BinaryWriter]::new($file)
try{
 $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count)
 $offset=6+16*$frames.Count
 foreach($frame in $frames){$dimension=if($frame.Size -eq 256){0}else{$frame.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.Bytes.Length}
 foreach($frame in $frames){$writer.Write([byte[]]$frame.Bytes)}
}finally{$writer.Dispose()}
