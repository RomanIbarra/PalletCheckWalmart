   M S B A M L   `   `   `  ����F  BPalletCheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null;"clr-namespace:PalletCheck.ControlsPalletCheck.Controls  5      /    )PalletCheck.Controls.CaptureBufferBrowser   5      U QScottPlot.WPF, Version=4.1.27.0, Culture=neutral, PublicKeyToken=e53b06131e34a3aaR NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5      R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5      Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     5      @d2http://schemas.microsoft.com/expression/blend/2008     5      4local"clr-namespace:PalletCheck.Controls     5      
����� �$��2q�5   !   .��5
      �   � ColumnDefinitions  5   
   �� 5      $��60 �6   �� 5      $	��47* �6    � RowDefinitions 5   
   �� 5      $��40 �6   �� 5      $
��209* �6   �� 5      $��40 �6   .+�5   
   ��  �� Orientation$ 
Horizontal=�6   $��2��6.   ��5      �� 5       �� EndPoint$ 0.5,1&�6&    �� 
StartPoint$ 0.5,0&�67   .��5      �  � Color$ Black��6#    � Offset$ 0Q�61   � 5      $ 	#FF003E5B��6#   $ 1Q�65   5      .+�5      �� -   5        $
��Save��5      $	��100��6$   $��5q�60   ���	#FF10B3CD6R   5      �� 5    
   $��1��6   $ 
Horizontal=�6"   $��2��6;   .+�5!      �� -   5         ��Name$ sldHighRange��5!       �� Orientation$ Vertical=�6+   	 � Value$	 1Q�6B   $��5q�6L   
 � Minimum$
 0Q�6W    � Maximum$ 1Q�6c   6o   �� 5"      -   5        $ sldLowRange��5"      $ Vertical=�6*   $	 0Q�6A   $��5q�6K   $
 0Q�6V   $ 1Q�6b   6n   5#      �� 5%   
   -   5         ��Name$ OuterCanvas��5%      $��1��6&   }�. $��1��6G   $��2��6W    � Cursor$ Cross��6h   $��Stretch=�6w   $��Stretch=�6�   ��5&      �� 5'      .+�5)      �� -   5        $ ImageCanvas��5)      ���#101010}�.  $ Cross��6T   $��Stretch=�6c   $��Stretch=�6�   .+�5*      �� -   5         ��Name$ 	CapBufImg��5*      $
��Fill=�6+   5+      5,      �� 5.   
   -   5         ��Name$ tbInfo��5.      $��1��6$   $��12(�64   
�������$��	63,0,10,0q�6V   $��Center=�6i   $��0, 0, 0��6�   50      51      