Imports System.IO
Imports System.Text.Json
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Controls.Primitives
Imports System.Windows.Forms
Imports System.Windows.Input
Imports System.Windows.Media
Imports System.Windows.Media.Imaging



Class MainWindow

    Private Notes As New List(Of NoteData)
    Private SelectedColor As String = "Yellow"
    Private NoteIdCounter As Integer = 1
    Private LastClickTime As DateTime = DateTime.MinValue
    Private SaveFile As String = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "StickyNotesWpf",
        "notes.json"
    )

    Private DraggingBorder As Border = Nothing
    Private DragOffset As Point
    Private DragNoteId As Integer = -1

    Private Sub Window_Loaded(sender As Object, e As RoutedEventArgs)
        StretchAcrossAllMonitors()
        LoadNotes()
    End Sub

    Private Sub StretchAcrossAllMonitors()
        Me.WindowStyle = WindowStyle.None
        Me.ResizeMode = ResizeMode.NoResize
        Me.WindowState = WindowState.Normal
        Dim minLeft As Integer = Integer.MaxValue
        Dim minTop As Integer = Integer.MaxValue
        Dim maxRight As Integer = Integer.MinValue
        Dim maxBottom As Integer = Integer.MinValue

        For Each scr As Screen In Screen.AllScreens
            Dim wa = scr.WorkingArea
            If wa.Left < minLeft Then minLeft = wa.Left
            If wa.Top < minTop Then minTop = wa.Top
            If wa.Right > maxRight Then maxRight = wa.Right
            If wa.Bottom > maxBottom Then maxBottom = wa.Bottom
        Next

        Dim source = PresentationSource.FromVisual(Me)
        If source IsNot Nothing Then
            Dim m = source.CompositionTarget.TransformFromDevice
            Dim topLeft = m.Transform(New Point(minLeft, minTop))
            Dim bottomRight = m.Transform(New Point(maxRight, maxBottom))

            Me.Left = topLeft.X
            Me.Top = topLeft.Y
            Me.Width = bottomRight.X - topLeft.X
            Me.Height = bottomRight.Y - topLeft.Y
        End If
    End Sub

    Private Sub ColorButton_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, System.Windows.Controls.Button)
        If btn Is Nothing Then Return
        SelectedColor = btn.Tag.ToString()
    End Sub

    Private Sub WorkspaceCanvas_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        If e.ClickCount = 2 AndAlso e.OriginalSource Is WorkspaceCanvas Then
            Dim p = e.GetPosition(WorkspaceCanvas)

            Dim note As New NoteData With {
                .Id = NoteIdCounter,
                .ColorName = SelectedColor,
                .X = Math.Max(10, p.X - 110),
                .Y = Math.Max(10, p.Y - 110),
                .Text = ""
            }

            NoteIdCounter += 1
            Notes.Add(note)
            CreateNoteElement(note, True)
            SaveNotes()
        End If
    End Sub

    Private Sub CreateNoteElement(note As NoteData, focusEditor As Boolean)
        If note.Width <= 0 Then note.Width = 220
        If note.Height <= 0 Then note.Height = 220

        Dim outer As New Border With {
        .Width = note.Width,
        .Height = note.Height,
        .MinWidth = 160,
        .MinHeight = 160,
        .CornerRadius = New CornerRadius(3),
        .Background = GetBrushFromName(note.ColorName),
        .Tag = note.Id
    }

        outer.Effect = New Media.Effects.DropShadowEffect With {
        .BlurRadius = 8,
        .ShadowDepth = 2,
        .Opacity = 0.3
    }

        Dim rootGrid As New Grid
        rootGrid.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
        rootGrid.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})

        Dim header As New DockPanel With {
    .Height = 28,
    .Margin = New Thickness(10, 10, 10, 8),
    .LastChildFill = True,
    .Cursor = System.Windows.Input.Cursors.SizeAll,
    .Background = Brushes.Transparent
    }

        Dim deleteBtn As New System.Windows.Controls.Button With {
        .Content = "×",
        .Width = 24,
        .Height = 24,
        .FontSize = 16,
        .Foreground = Brushes.White,
        .Background = New SolidColorBrush(Color.FromArgb(80, 0, 0, 0)),
        .BorderThickness = New Thickness(0),
        .Cursor = System.Windows.Input.Cursors.Hand
    }

        DockPanel.SetDock(deleteBtn, Dock.Right)
        header.Children.Add(deleteBtn)

        Dim editor As New System.Windows.Controls.TextBox With {
        .Text = note.Text,
        .Margin = New Thickness(15, 0, 20, 20),
        .BorderThickness = New Thickness(0),
        .Background = Brushes.Transparent,
        .AcceptsReturn = True,
        .TextWrapping = TextWrapping.Wrap,
        .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        .FontSize = 14
    }

        Dim resizeThumb As New System.Windows.Controls.Primitives.Thumb With {
        .Width = 14,
        .Height = 14,
        .HorizontalAlignment = HorizontalAlignment.Right,
        .VerticalAlignment = VerticalAlignment.Bottom,
        .Cursor = System.Windows.Input.Cursors.SizeNWSE,
        .Margin = New Thickness(0, 0, 4, 4),
        .Opacity = 0.7,
        .Background = Brushes.Gray
    }

        Grid.SetRow(header, 0)
        Grid.SetRow(editor, 1)
        Grid.SetRow(resizeThumb, 1)

        rootGrid.Children.Add(header)
        rootGrid.Children.Add(editor)
        rootGrid.Children.Add(resizeThumb)

        outer.Child = rootGrid

        Canvas.SetLeft(outer, note.X)
        Canvas.SetTop(outer, note.Y)
        WorkspaceCanvas.Children.Add(outer)

        AddHandler deleteBtn.Click,
        Sub()
            WorkspaceCanvas.Children.Remove(outer)
            Notes.RemoveAll(Function(n) n.Id = note.Id)
            SaveNotes()
        End Sub

        AddHandler editor.TextChanged,
        Sub()
            note.Text = editor.Text
            SaveNotes()
        End Sub

        AddHandler resizeThumb.DragDelta,
        Sub(s, ev)
            Dim newWidth As Double = outer.Width + ev.HorizontalChange
            Dim newHeight As Double = outer.Height + ev.VerticalChange

            If newWidth < outer.MinWidth Then newWidth = outer.MinWidth
            If newHeight < outer.MinHeight Then newHeight = outer.MinHeight

            outer.Width = newWidth
            outer.Height = newHeight

            note.Width = outer.Width
            note.Height = outer.Height

            SaveNotes()
        End Sub

        AddHandler header.MouseLeftButtonDown, AddressOf NoteHeader_MouseLeftButtonDown
        AddHandler header.MouseMove, AddressOf NoteHeader_MouseMove
        AddHandler header.MouseLeftButtonUp, AddressOf NoteHeader_MouseLeftButtonUp

        If focusEditor Then
            editor.Focus()
            editor.CaretIndex = editor.Text.Length
        End If
    End Sub
    Private Sub NoteHeader_MouseLeftButtonDown(sender As Object, e As System.Windows.Input.MouseButtonEventArgs)
        Dim header = TryCast(sender, DockPanel)
        If header Is Nothing Then Return

        Dim border = TryCast((TryCast(header.Parent, Grid))?.Parent, Border)
        If border Is Nothing Then Return

        DraggingBorder = border
        DragNoteId = CInt(border.Tag)
        DragOffset = e.GetPosition(DraggingBorder)
        header.CaptureMouse()
    End Sub

    Private Sub NoteHeader_MouseMove(sender As Object, e As System.Windows.Input.MouseEventArgs)
        If DraggingBorder Is Nothing OrElse e.LeftButton <> MouseButtonState.Pressed Then Return

        Dim p = e.GetPosition(WorkspaceCanvas)
        Canvas.SetLeft(DraggingBorder, p.X - DragOffset.X)
        Canvas.SetTop(DraggingBorder, p.Y - DragOffset.Y)
    End Sub

    Private Sub NoteHeader_MouseLeftButtonUp(sender As Object, e As System.Windows.Input.MouseButtonEventArgs)
        If DraggingBorder Is Nothing Then Return

        Dim note = Notes.FirstOrDefault(Function(n) n.Id = DragNoteId)
        If note IsNot Nothing Then
            note.X = Canvas.GetLeft(DraggingBorder)
            note.Y = Canvas.GetTop(DraggingBorder)
            SaveNotes()
        End If

        Dim header = TryCast(sender, DockPanel)
        If header IsNot Nothing Then header.ReleaseMouseCapture()

        DraggingBorder = Nothing
        DragNoteId = -1
    End Sub

    Private Function GetBrushFromName(name As String) As Brush
        Select Case name
            Case "Yellow"
                Return New SolidColorBrush(Color.FromRgb(&HFF, &HD9, &H66))
            Case "Pink"
                Return New SolidColorBrush(Color.FromRgb(&HFF, &H9E, &HCD))
            Case "Blue"
                Return New SolidColorBrush(Color.FromRgb(&H9E, &HC8, &HFF))
            Case "Green"
                Return New SolidColorBrush(Color.FromRgb(&HB4, &HF1, &HA4))
            Case "Orange"
                Return New SolidColorBrush(Color.FromRgb(&HFF, &HB3, &H66))
            Case "Purple"
                Return New SolidColorBrush(Color.FromRgb(&HD9, &HB3, &HFF))
            Case Else
                Return Brushes.LightYellow
        End Select
    End Function

    Private Sub SaveNotes()
        Try
            Dim folder = Path.GetDirectoryName(SaveFile)
            If Not Directory.Exists(folder) Then
                Directory.CreateDirectory(folder)
            End If

            Dim json = JsonSerializer.Serialize(Notes)
            File.WriteAllText(SaveFile, json)
        Catch
        End Try
    End Sub

    Private Sub LoadNotes()
        Try
            If Not File.Exists(SaveFile) Then Return

            Dim json = File.ReadAllText(SaveFile)
            Dim loaded = JsonSerializer.Deserialize(Of List(Of NoteData))(json)

            If loaded Is Nothing Then Return

            Notes = loaded
            If Notes.Count > 0 Then
                NoteIdCounter = Notes.Max(Function(n) n.Id) + 1
            End If

            For Each note In Notes
                CreateNoteElement(note, False)
            Next
        Catch
        End Try
    End Sub

End Class