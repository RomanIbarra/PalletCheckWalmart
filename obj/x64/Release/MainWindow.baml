   M S B A M L   `   `   `  ���� F  BPalletCheck, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:PalletCheckPalletCheck  ;"clr-namespace:PalletCheck.ControlsPalletCheck.Controls  b ^Sick.EasyRanger.Controls, Version=8.6.0.3066, Culture=neutral, PublicKeyToken=6c90f9b7bbb67420eHclr-namespace:Sick.EasyRanger.Controls;assembly=Sick.EasyRanger.ControlsSick.EasyRanger.Controls yRclr-namespace:Sick.EasyRanger.Controls.Converter;assembly=Sick.EasyRanger.Controls"Sick.EasyRanger.Controls.Converter     PalletCheck.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35U QScottPlot.WPF, Version=4.1.27.0, Culture=neutral, PublicKeyToken=e53b06131e34a3aaW SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     +localclr-namespace:PalletCheck     7controls"clr-namespace:PalletCheck.Controls     \controlHclr-namespace:Sick.EasyRanger.Controls;assembly=Sick.EasyRanger.Controls     i
convertersRclr-namespace:Sick.EasyRanger.Controls.Converter;assembly=Sick.EasyRanger.Controls     -     � Title$  PalletCheck��$
��1050��$
��1920�� � WindowStartupLocation$ CenterScreen=� � WindowState$ 	Maximized=� � 
ResizeMode$ 	CanResize=�
���   � � 	Resources    Transparent3DButtonStyle �� 
TargetType �� Property �� Value3  -System.Windows.Media.Effects.DropShadowEffect   Color	   
BlurRadius
   ShadowDepth   Opacity �� 
TargetType ��Name �� CornerRadius J� Effect �� Triggers P� Property P� Value �� 
TargetName��%�  &	        �� " ��.���  � ��
 �������  � ��$ 18(���  � ��$
 Bold"���  � ��
 ���� ��  � ��$ 2q���  � �� �	#FF177DC3��  �@J�Effect   $ Black��$	 10Q�$
 2Q�$	 0.3Q���  � ��$
 10,5q���  � �� �� " ��.R�� .+��� $ Border��#��z��#��z��#��z��$ 5��   $
 Gray��$	 8Q�$
 2Q�$	 0.5Q��� $��Center=�$��Center=� P�  �@J�IsMouseOver . . ��� $ Border�� � �� �	#FFCCE7FF��  � ��
 �   �P�  � �� . . ��� $ Border�� � �� �	#FF99C9FF��  � �� �	#FF0056A3P�  � {� .  . ���  � ��
 �������  � �� �	#FFE0E0E0��  � �� �	#FFA0A0A0.���  � ColumnDefinitions �� $��7 ��� $��96 ��� $��3* ��� $��* � � RowDefinitions �� $��50 ��� $��* ��� $��50 �.+�]� $��4��$��0��$��Stretch=�$��0,0,0,0q�$��45��$	��Top=�$��2�� J� OpacityMask
 �   �.+��� -    ��Name$ btnStart��#��[  $��START��$y�Hidden=�$��10��$��5q��� -   $ 	btnBypass��#��[  $��BYPASS��$	��200��$��5q��� -   $ 	btnRecord��#��[  $��RECORD��$y�Hidden=�$��10��$��5q��� #��["�$��	25,5,25,5q��� -   $ btnProcessPallet��#��[  $��PROCESS PALLET��$	��180��$��5q��� -   $ btnProcessRecording��#��[  $��PROCESS RECORDING��$	��220��$��5q��� #��["�$��	25,5,25,5q��� -   $ btnSettingsControl��#��[  $��SETTINGS��$	��120��$��5q��� -   $ btnStatusControl��#��[  $��STATUS��$	��120��$��5q��� -	    ��Name$ 
ModeStatus��$��32(�$
��Bold"�
�������$��0,0,0,0q� �� TextAlignment$ Center=�$	��200��
 �   �	RUNNING��  �� Orientation$ 
Horizontal=�$��Right=�$��Center=�� $��1��$��3��
 �   � �� $��2* ��� $��* � �� $��* ��� $��* �.+�� 
 �   � �� $��80 ��� $��* �.+��� 
�������$��2q�$��0��$��0��$��2��._��� $��Center=�$��Center=��� $��0��$��Center=�$	��Top=�$��1��$ 
Horizontal=�
 �   �.+��� -
   $ btnLoad_Top��#��[  $��	10,10,0,0q�{�.  $��Process Top��$	��Top=�$��15(�$	��120��
 �   ��� -   $ btnSetting_Top��$��	10,10,0,0q�$��Setting Top��#��[  $	��Top=�$��15(�$	��120��
 �   ��� -   $ btnShow_Top��$��	10,10,0,0q�$��Show 3D��#��[  $��15(�$	��120��� -    �Name$ CBB_Container��$��0��$��1��
���   �$��0,40,5,5q�$��1��
 �   ��� -   $ PalletName0��$��18(�$
��Bold"�
�������$��0��$#��0,10                    ,30,0q�$	��Top=�$��Right=� J� Opacity$ 1Q�$��1��
 �   ��� $
�Info���� -    ��Name$ CBB_Button_List��$��5,50,5,5q�$ Vertical=�$��0��
 �   �� $��0��$��1��
 �   � �� $��80 ��� $��* �.+��� 
�������$��2q�$��0��$��0��$��2��._��� $��Center=�$��Center=��� $��0��$��Center=�$	��Top=�$��1��$ 
Horizontal=�
 �   �.+��� -   $ btnLoad_Bottom��$��	10,10,0,0q�#��[  $	��Top=�{�.  $��15(�$	��120��
 �   �$��Process Bottom���� -   $ btnSetting_Bottom��$��	10,10,0,0q�$��Setting Bottom��#��[  $	��Top=�$��15(�$	��120��
 �   ��� -   $ btnShow_Bottom��$��	10,10,0,0q�$��Show 3D��#��[  $��15(�$	��120��� -   $ CBB_Container1��$��0��$��1��
���   �$��0,40,5,5q�$��1��
 �   ��� -   $ PalletName1��$��18(�$
��Bold"�
�������$��0��$#��0,10                    ,30,0q�$	��Top=�$��Right=�$ 1Q�$��1��
 �   ��� $
�Info���� -   $ CBB_Button_List1��$��5,50,5,5q�$ Vertical=�$��0��
 �   ��� 
�������$��2q�$��1��$��0��._��� $��Center=�$��Center=�� $��1��$��0��
 �   � �� $��80 ��� $��* �.+��� $��0��$��1��$ 
Horizontal=�$	��Top=�$��Center=�
 �   �.+��� -   $ btnLoad_Left��$��	10,10,0,0q�{�.  $��Process Left��#��[  $	��Top=�$��15(�$	��120��
 �   ��� -   $ btnSetting_Left��$��	10,10,0,0q�$��Setting Left��#��[  $	��Top=�$��15(�$	��120��
 �   ��� -   $ btnShow_Left��$��	10,10,0,0q�$��Show 3D��#��[  $��15(�$	��120��,  &Sick.EasyRanger.Controls.View2DControl  -     Name$ 
ViewerLeft��$��0��$��0��$��5,40,5,5q�$��2��
����������	#FF9E3232�� -   $ PalletName2��$��18(�$
��Bold"�
�������$��0��$#��0,10                    ,30,0q�$	��Top=�$��Right=�$ 1Q�$��1��
 �   ��� $
�Info���� -   $ CBB_Button_List2��$ Vertical=�$��0��
 �   ��� 
�������$��2q�$��1��$��2��._��� $��Center=�$��Center=�� $��1��$��1��
 �   � �� $��80 ��� $��* �.+��� $��0��$ 
Horizontal=�$	��Top=�$��Center=�$��1��
 �   �.+��� -   $ btnLoad_Right��$��	10,10,0,0q�{�.  $��Process Right��#��[  $	��Top=�$��15(�$	��120��
 �   ��� -   $ btnSetting_Right��$��	10,10,0,0q�$��Setting Right��#��[  $	��Top=�$��15(�$	��120��
 �   ��� -   $ btnShow_Right��$��	10,10,0,0q�$��Show 3D��#��[  $��15(�$	��120��  -   $ ViewerRight��$��0��$��0��$��5,40,5,5q�$��2��
 �   �
��������� -    $ PalletName3��$��18(�$
��Bold"�
�������$��0��$#��0,10                    ,30,0q� K� ZIndex$	 100��$	��Top=�$��Right=�$ 1Q�$��1��
 �   ��� $
�Info���� -!   $ CBB_Button_List3��$ Vertical=�$��0��
 �   �� $��1��$��3��$��2��$��0,0,0,0q�
 �   �
���   � �� $
��800* ��� $	��60* ��� $
��200* �.+��� -"   $ TotalResultTextBlock��$��Result��
�������$��80(�$��Heavy"�$	��Top=�$��10,0,0,0q��� -#   $ 
btnDefects��$��
0,109,10,0q�#��[  $	��Top=�$��25(�$	��150��
 �   �$��Right=�.��	DEFECTS&   System.Windows.Controls.DataGrid  -$   $��defectTable��$��0��    AlternatingRowBackground
  ��ح�! �� AlternationCount$! 2��"   AutoGenerateColumns" .  $��10,152,10,0q�$��Normal"�#   ColumnHeaderHeight$# 30Q�$   	RowHeight$$ 30Q�
 �   �$% �� IsSynchronizedWithCurrentItem$
% TrueY���Z� &   RowStyle& �� )  #System.Windows.Controls.DataGridRow"  .���  � ��$
 Bold"���  � ��$ 16(�'   ColumnHeaderStyle' �� =  7System.Windows.Controls.Primitives.DataGridColumnHeader"  .���  � ��$
 Bold"���  � ��$ 16(�(   Columns( 0  *System.Windows.Controls.DataGridTextColumn  ,  &System.Windows.Controls.DataGridColumn)   Header$) Board��*   Width5  /System.Windows.Controls.DataGridLengthConverter$* 75 +   
IsReadOnly+ . 1	  +System.Windows.Controls.DataGridBoundColumn, 	  Binding, �� *
Location+  $) Defect��$	* 100 + . , �� *Code+  $) Comment��$* * + . , �� *	Comment+�� -%   $ btnStatistics��$��1��$��	0,10,10,0q�#��[  $	��Top=�$��25(�$	��150��
 �   �$��Right=�.��Reset  -&   $��statisticsTable��$��2��
  ��ح�$! 2��" .  $��Stretch=�
 �   ���Z� & �� "  .���  � ��$
 Bold"���  � ��$ 16(�' �� "  .���  � ��$
 Bold"���  � ��$ 16(�(   $) Description��$* * + . , �� *Description+  $) Count��$* * + . , �� *Count1+  $) %��$* * + . , �� *
Percent1+� -'   - �Name$- ProgressBar��$��46��$��
10,10,10,5q�$��Bottom=�$y�Hidden=�$��1��
 �   ����� " �.R��� 
���   �$��3q�
�������._�� $ 
PART_Track��.+��� . ��Name$. PART_Indicator��$
��Left=����	#FF2D5774�� -(   $ ProgressBarText��$��Center=�$��	0,448,0,0q�$��1��$
��Wrap=�$��Progress Bar Text��$	��Top=�
�������$
��Bold"�$��16(�
 �   ��� $	��300��$	��100��$��3��$	��Top=�$��Right=�$��2��$��/LogoWalmart.png��� $��1q�$��3��$��0��$��4��
���   � �� $��* ��� $
��Auto �.+��� $ 
Horizontal=�$��0��$��0,5,0,5q�.+��� $ 
Horizontal=�$��0,5,0,5q�.+��� -)   $ Camera1StatusText��$��C1 Searching ��$��Center=�$��18(�
�������$
��Bold"�A� -*   / A�Name$/ Camera1StatusIndicator��$��20��$��20��
���  ��
���   �$��1��$��0,0,10,0q��� $ 
Horizontal=�$��0,5,0,5q�.+��� -+   $ Camera2StatusText��$��C2 Searching��$��Center=�$��18(�
�������$
��Bold"�A� -,   $/ Camera2StatusIndicator��$��20��$��20��
���  ��
���   �$��1��$��0,0,10,0q��� $ 
Horizontal=�$��0,5,0,5q�.+��� --   $ Camera3StatusText��$��C3 Searching��$��Center=�$��18(�
�������$
��Bold"�A� -.   $/ Camera3StatusIndicator��$��20��$��20��
���  ��
���   �$��1��$��0,0,10,0q��� $ 
Horizontal=�$��0,5,0,5q�.+��� -/   $ Camera4StatusText��$��C4 Searching��$��Center=�$��18(�
�������$
��Bold"�A� -0   $/ Camera4StatusIndicator��$��20��$��20��
���  ��
���   �$��1��$��0,0,10,0q��� $ 
Horizontal=�$��0,5,0,5q�.+��� -1   $ Camera5StatusText��$��C5 Searching��$��Center=�$��18(�
�������$
��Bold"�A� -2   $/ Camera5StatusIndicator��$��20��$��20��
���  ��
���   �$��1��$��0,0,10,0q��� $ 
Horizontal=�$��0,5,0,5q�.+��� -3   $ Camera6StatusText��$��C6 Searching��$��Center=�$��18(�
�������$
��Bold"�A� -4   $/ Camera6StatusIndicator��$��20��$��20��
���  ��
���   �$��1��$��0,0,10,0q��� -5   $ LogText��$��1��$��Center=�$��Right=�
�������$
��SICK��$��20(�$��	10,0,10,0q��� -6   $ CurDateTime��$��1��$��24(�$
��Bold"�
�������$��0,0,50,0q�$��Center=�$��Right=�
���   �Time