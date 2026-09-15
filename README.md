# PhoneGamepad

出差住的酒店没有椅子，只能在床上玩电脑，用键鼠很不舒服，但是没有带手柄！然后就去网上找模拟手柄的程序，但是要么收费要么不好用！折腾半天受不了了，我就想为啥我不自己做一个，反正这个电脑上配好了visual studio，虽然之前几乎没做过app，但是AI真的太好用了你知道吗！  
  
app现在很简陋，但是感觉完全能用！当然肯定没真的手柄那么舒服就是了！

## 使用方法

### 手机端

1、安装app  
2、用数据线连接电脑  
3、进入设置点击六下安卓/OS版本进入开发者模式，开启USB调试  

### 电脑端
1、解压压缩包，里面是程序exe、虚拟手柄驱动、安卓platform-tools（SDK，用adb）  
2、安装驱动ViGEmBus，重启一下  
3、启动PhoneGamepadReceiver.exe，platform-tools文件夹要和程序在同一目录下  
  
驱动来自https://github.com/nefarius/ViGEmBus/

### 通信端口
用的adb通讯，端口5066，注意不要被占用了，后面也许会加设置端口的功能  
<br>

## 环境方面
### PhoneGamepad
安卓14 + net8.0
### PhoneGamepadReceiver
1、ViGEmClient文件夹里面include、src来自ViGEmBus源码，依赖项加include就行了  
2、链接器附加库目录加ViGEmClient\lib\debug\x64，这是ViGEmBus源码编译出来的
