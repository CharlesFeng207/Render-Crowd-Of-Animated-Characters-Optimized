顶点动画是一种空间换时间的策略，特别适用于移动平台上大规模角色渲染。基本上顶点动画的性能取决于动画贴图的采样，这张贴图的体积决定了顶点动画运作时的带宽，在大量使用时包体、内存也是一个问题。本项目基于chenjd的方案，提供三种实用方式对其进行优化，使其动画贴图体积降低至原来的十分之一。

知乎文章链接
https://zhuanlan.zhihu.com/p/571080841

![pic](https://github.com/CharlesFeng207/Render-Crowd-Of-Animated-Characters-Optimized/blob/master/Pic/v2-f74a1439d2c30ad20f5838ca4d589011_720w.webp)
