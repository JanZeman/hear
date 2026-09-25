// Native haptic, wrapping UIImpactFeedbackGenerator instead of Unity's Handheld.Vibrate(), which
// on iOS triggers a fixed-length/fixed-intensity system buzz with no way to tune it from C#.
// Human feedback 2026-09-26: the Tide Troubles capture haptic should be gentler/shorter than
// Handheld.Vibrate() - but the Light style tried first was "de facto gone", imperceptible -
// Medium is the "neco mezi" middle ground between the two. Unity auto-includes any .mm file
// under Assets/Plugins/iOS/ into the generated Xcode project, no extra build configuration
// needed.
#import <UIKit/UIKit.h>

extern "C" {

void _HearLightHaptic(void)
{
    if (@available(iOS 10.0, *)) {
        UIImpactFeedbackGenerator *generator =
            [[UIImpactFeedbackGenerator alloc] initWithStyle:UIImpactFeedbackStyleMedium];
        [generator prepare];
        [generator impactOccurred];
    }
}

}
