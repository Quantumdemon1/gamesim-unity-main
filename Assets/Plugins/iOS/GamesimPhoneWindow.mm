#import <UIKit/UIKit.h>
#import "UnityAppController.h"

extern "C" float GamesimWindowLongEdgePoints()
{
    UIView *view = (UIView *)GetAppController().unityView;
    CGSize size = view.bounds.size;
    return (float)MAX(size.width, size.height);
}
