## Running under Macos

There's unclear issue with OpenCL path resolution, which I suppose is a netcore issue.

In order to fix the issue run the following command before executing application:

```bash
export DYLD_LIBRARY_PATH=$DYLD_LIBRARY_PATH:/System/Library/Frameworks/OpenCL.framework
```