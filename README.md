# WPFMidiBand

C# 课程实验3

![](https://raw.githubusercontent.com/8qwe24657913/WPFMidiBand_SRC/master/Images/WPF.png)

## 功能概述

除简单播放外，还实现了：

1. 单曲循环、列表循环功能
2. 手动切换乐器界面与播放列表界面，可点击播放列表切歌
3. 简化了界面和操作逻辑，使之比起代码逻辑本身更符合人类的操作习惯
4. 使界面半透明，更具美感
5. 计算总时长和已播放时长，并实时更新标题，方便用户直观观察播放时间
6. 解决了原程序出现死锁问题的bug
7. 可直接拖拽文件到窗口内来播放
8. 添加了任务栏显示播放进度和控制播放功能，进一步提升了美观性和实用性

**实现细节**

1. 关于循环播放

与 WindowsForm 不同，WPF 使用 Dispatcher.Invoke() 和 Dispatcher.BeginInvoke() 在其它线程运行代码，但基本原理大同小异，MIDI 播放器为了保证操作的原子性，在内部使用了锁，而对 MIDI 播放器的调用只能在主线程，需要 Invoke() 方法，Invoke() 方法会打断目标线程正在执行的代码，直到操作完成，如果 Invoke() 时恰好 MIDI 的锁被占用，Action 获取不到锁，就会等待锁的释放，而锁的释放又要等待 Invoke() 完成，于是造成了程序的假死现象

![img](https://raw.githubusercontent.com/8qwe24657913/WPFMidiBand_SRC/master/Images/lock.png) 

2. 关于时间显示

与 WindowsForm 中计算方法完全相同，不再赘述

![img](https://raw.githubusercontent.com/8qwe24657913/WPFMidiBand_SRC/master/Images/tempo.png) 

3. 关于透明的实现

WPF 实现透明功能远比 WindowsForm 简单得多，只需要将

```xaml
<Window AllowsTransparency="True" WindowStyle="None">
```

这两个属性加到 Window 标签上即可允许透明，控件可以设置 rgba 背景色（WIndowsForm 不能）

为了让边缘更加圆滑（研究表明人眼在处理尖锐棱角时比处理圆角要慢，合适的圆角是符合人类习惯的设计），我们只需要加上这样

```xaml
<Border CornerRadius="10,10,10,10" Name="RoundCornerBorder" Background="#7F000000" Margin="10">
```

一个元素将整个内容区域包裹即可，既使用 CornerRadius 设置了圆角，又提供了半透明背景色，还添加了合适的边缘

为了给窗口添加一个阴影效果以使其更易与其它窗口区分，只需要再添加

```xaml
<Border.Effect>
	<DropShadowEffect ShadowDepth="0" Color="#7F000000" BlurRadius="10"/>
</Border.Effect>
```

这段代码即可

![img](https://raw.githubusercontent.com/8qwe24657913/WPFMidiBand_SRC/master/Images/shadow.png) 

此外还要注意，使用透明时不能使用 Windows 自带的窗口标题栏，标题栏需要自己绘制

4. 关于任务栏上的进度和播放控制

WPF 对此功能提供了很好的支持，只需要在 XAML 中添加代码

```xaml
    <Window.TaskbarItemInfo>

        <TaskbarItemInfo x:Name="taskBarItemInfo">

            <TaskbarItemInfo.ThumbButtonInfos>

                <ThumbButtonInfoCollection>

                    <ThumbButtonInfo

                        DismissWhenClicked="True"

                        Click="thumbOpen_Click"

                        Description="Select files to play"

                        ImageSource="Images/add.png"/>

                    <ThumbButtonInfo

                        DismissWhenClicked="True"

                        Click="thumbPlay_Click"

                        Description="Play"

                        ImageSource="Images/play.png"/>

                    <ThumbButtonInfo

                        DismissWhenClicked="True"

                        Click="thumbPause_Click"

                        Description="Pause"

                        ImageSource="Images/pause.png"/>

                </ThumbButtonInfoCollection>

                </TaskbarItemInfo.ThumbButtonInfos>

        </TaskbarItemInfo>

</Window.TaskbarItemInfo>

```

即可实现任务栏播放控制，在代码中操作 taskBarItemInfo.ProgressState 和 taskBarItemInfo.ProgressValue 即可实现任务栏进度条

![img](https://raw.githubusercontent.com/8qwe24657913/WPFMidiBand_SRC/master/Images/taskbar.png) 

taskBarItemInfo.ProgressState 的可能值如上图

taskBarItemInfo.ProgressValue 为进度百分比

DismissWhenClicked 若设置为 True，则点击按钮后隐藏预览，反之不隐藏预览

这里需要注意任务栏播放控制的图片需要自己制作，建议保持风格的一致（扁平风格）以达到更好的用户体验

## 项目特色

☑ 功能丰富

☑ 易于操作

☑ 美观大方

☑ 实用性强

## 代码总量

约 1000+ 行

## 工作时间

约两晚

## 结论

1. WPF 实现透明远比 Winform 的奇技淫巧简单，效果也更好
2. 采用可作为文本编辑的 XAML 构建界面比 WinForm 只能通过设计器修改的灵活性更强，也更加直观