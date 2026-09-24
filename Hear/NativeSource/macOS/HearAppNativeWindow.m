// Native macOS plugin: sets the real NSWindow content minimum size so the OS itself refuses to
// drag the window smaller, with zero repositioning/"jump" side effect. This is the correct
// platform-level replacement for snapping Screen.SetResolution after the fact at the Unity
// level, which visibly moved the window because Unity has no control over which corner/edge the
// OS anchors a resize to.
//
// Build (already built into ../libHearAppNativeWindow.dylib, kept here for reference/rebuilds):
//   clang -dynamiclib -framework Cocoa -o libHearAppNativeWindow.dylib HearAppNativeWindow.m

#import <Cocoa/Cocoa.h>

void HearApp_SetMinWindowSize(float width, float height)
{
    NSArray<NSWindow *> *windows = [NSApp windows];
    for (NSWindow *window in windows)
    {
        if ([window isVisible])
        {
            [window setContentMinSize:NSMakeSize(width, height)];
        }
    }
}
