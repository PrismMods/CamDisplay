// CamDisplayLaunch: run a program as its own TCC "responsible process".
//
// Steam (and MelonLoader's melonloader-launch.sh, which execs the game directly) makes the game a
// child of steam_osx, so macOS asks Steam's entitlements about the camera, and Steam only declares
// the microphone. Spawning with responsibility disclaimed makes the game answer for itself, while
// it stays in Steam's process tree so play time, the overlay and Stop keep working.
//
// Usage: CamDisplayLaunch <program> [args...]   (exit status = the child's; 128+signo if signalled)

#include <dlfcn.h>
#include <errno.h>
#include <signal.h>
#include <spawn.h>
#include <stdio.h>
#include <string.h>
#include <sys/wait.h>
#include <unistd.h>

extern char **environ;

static pid_t child = 0;

static void forward(int sig) {
    if (child > 0) kill(child, sig);
}

int main(int argc, char **argv) {
    if (argc < 2) {
        fprintf(stderr, "usage: CamDisplayLaunch <program> [args...]\n");
        return 64;
    }

    posix_spawnattr_t attr;
    posix_spawnattr_init(&attr);
    // Private libsystem symbol (used by terminals for the same reason); looked up so a macOS
    // without it still launches the game, just without the camera fix.
    int (*disclaim)(posix_spawnattr_t *, int) =
        (int (*)(posix_spawnattr_t *, int))dlsym(RTLD_DEFAULT, "responsibility_spawnattrs_setdisclaim");
    if (disclaim) disclaim(&attr, 1);
    else fprintf(stderr, "CamDisplayLaunch: responsibility disclaim unavailable; camera may be denied\n");

    int err = posix_spawn(&child, argv[1], NULL, &attr, argv + 1, environ);
    posix_spawnattr_destroy(&attr);
    if (err != 0) {
        fprintf(stderr, "CamDisplayLaunch: %s: %s\n", argv[1], strerror(err));
        return 127;
    }

    // Steam's Stop signals the process it started, which is us: pass it on.
    struct sigaction sa;
    memset(&sa, 0, sizeof sa);
    sa.sa_handler = forward;
    sigaction(SIGTERM, &sa, NULL);
    sigaction(SIGINT, &sa, NULL);
    sigaction(SIGHUP, &sa, NULL);

    int status;
    while (waitpid(child, &status, 0) < 0) {
        if (errno != EINTR) return 1;
    }
    if (WIFEXITED(status)) return WEXITSTATUS(status);
    if (WIFSIGNALED(status)) return 128 + WTERMSIG(status);
    return 1;
}
