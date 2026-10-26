# Replay video encoder

The Windows client carries `ffmpeg.exe` in this directory so replay exports do not depend on the launching shell's PATH. Client builds copy this directory, including these notices. The binary is excluded from Git; provision it before building a client.

Current local distribution: FFmpeg `2024-08-01-git-bcf08c1171-full_build-www.gyan.dev`, from the user's existing installation. SHA256: `192ace815cfaba18c9fdea7f0ec1386c696cfcc474f389f066e36e4eead23cca`.

Upstream distribution and build information: https://www.gyan.dev/ffmpeg/builds/
FFmpeg source revision: https://github.com/FFmpeg/FFmpeg/commit/bcf08c1171

The original distribution's `LICENSE` and `README.txt` are retained beside the executable. This executable is a separate GPL v3 program; its build configuration and dependency list are available from `ffmpeg.exe -version` and the original README. It is invoked as a separate process for H.264 video encoding.
