   M S B A M L   `   `   `  ���� F  BPalletCheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null;"clr-namespace:PalletCheck.ControlsPalletCheck.Controls  /    )PalletCheck.Controls.CaptureBufferBrowser   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35U QScottPlot.WPF, Version=4.1.27.0, Culture=neutral, PublicKeyToken=e53b06131e34a3aaW SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     @d2http://schemas.microsoft.com/expression/blend/2008     4local"clr-namespace:PalletCheck.Controls     
����� �$��2q�.���   � ColumnDefinitions  �� $��60 ��� $	��47* � � RowDefinitions �� $��40 ��� $
��209* ��� $��40 �.+���  �� Orientation$ 
Horizontal=�$��2������  �� EndPoint$ 0.5,1&� �� 
StartPoint$ 0.5,0&�.���  � Color$ Black�� � Offset$ 0Q�� $ 	#FF003E5B��$ 1Q�.+��� -   $
��Save��$	��100��$��5q����	#FF10B3CD�� $��1��$ 
Horizontal=�$��2��.+��� -    ��Name$ sldHighRange�� �� Orientation$ Vertical=�	 � Value$	 1Q�$��5q�
 � Minimum$
 0Q� � Maximum$ 1Q��� -   $ sldLowRange��$ Vertical=�$	 0Q�$��5q�$
 0Q�$ 1Q��� -    ��Name$ OuterCanvas��$��1��}�. $��1��$��2�� � Cursor$ Cross��$��Stretch=�$��Stretch=����� .+��� -   $ ImageCanvas�����#101010}�.  $ Cross��$��Stretch=�$��Stretch=�.+��� -    ��Name$ 	CapBufImg��$
��Fill=��� -    ��Name$ tbInfo��$��1��$��2��$��18(�
�������$��20,0,0,0q�$��Center=�$��0, 0, 0��