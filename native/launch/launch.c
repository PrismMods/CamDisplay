// CamDisplayLaunch: run a program as its own TCC "responsible process".
//
// Steam (and the MelonLoader launch wrappers, which exec the game directly) makes the game a child
// of steam_osx, so macOS asks Steam's entitlements about the camera, and Steam only declares the
// microphone. Spawning with responsibility disclaimed makes the game answer for itself.
//
// POSIX_SPAWN_SETEXEC replaces this process instead of forking, so the game keeps the pid Steam
// started: play time and Stop work on the game itself. (A spawned child left Steam asking a
// non-app process to quit, and Stop took ~30 s to escalate.)
//
// Usage: CamDisplayLaunch <program> [args...]   (on success this process becomes <program>)

#include <dlfcn.h>
#include <spawn.h>
#include <stdio.h>
#include <string.h>

extern char **environ;

int main(int argc, char **argv) {
    if (argc < 2) {
        fprintf(stderr, "usage: CamDisplayLaunch <program> [args...]\n");
        return 64;
    }

    posix_spawnattr_t attr;
    posix_spawnattr_init(&attr);
    posix_spawnattr_setflags(&attr, POSIX_SPAWN_SETEXEC);
    // Private libsystem symbol (used by terminals for the same reason); looked up so a macOS
    // without it still launches the game, just without the camera fix.
    int (*disclaim)(posix_spawnattr_t *, int) =
        (int (*)(posix_spawnattr_t *, int))dlsym(RTLD_DEFAULT, "responsibility_spawnattrs_setdisclaim");
    if (disclaim) disclaim(&attr, 1);
    else fprintf(stderr, "CamDisplayLaunch: responsibility disclaim unavailable; camera may be denied\n");

    pid_t unused;
    int err = posix_spawn(&unused, argv[1], NULL, &attr, argv + 1, environ);
    // Only reached when the exec failed.
    fprintf(stderr, "CamDisplayLaunch: %s: %s\n", argv[1], strerror(err));
    return 127;
}
