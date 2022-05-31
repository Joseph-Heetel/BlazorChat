var __awaiter = (this && this.__awaiter) || function (thisArg, _arguments, P, generator) {
    function adopt(value) { return value instanceof P ? value : new P(function (resolve) { resolve(value); }); }
    return new (P || (P = Promise))(function (resolve, reject) {
        function fulfilled(value) { try { step(generator.next(value)); } catch (e) { reject(e); } }
        function rejected(value) { try { step(generator["throw"](value)); } catch (e) { reject(e); } }
        function step(result) { result.done ? resolve(result.value) : adopt(result.value).then(fulfilled, rejected); }
        step((generator = generator.apply(thisArg, _arguments || [])).next());
    });
};
class InfiniteListHelper {
    constructor(scrollEl, topEl, bottomEl, dnetobj, callbackTopInView, callbackBottomInView) {
        this.scrollEl = scrollEl;
        this.topEl = topEl;
        this.bottomEl = bottomEl;
        this.dnetobj = dnetobj;
        this.callbackTopInView = callbackTopInView !== null && callbackTopInView !== void 0 ? callbackTopInView : "JS_TopInView";
        this.callbackBottomInView = callbackBottomInView !== null && callbackBottomInView !== void 0 ? callbackBottomInView : "JS_BottomInView";
        this.observer = new IntersectionObserver((entries) => this.onIntersect(entries), {
            root: this.scrollEl
        });
        this.observer.observe(this.topEl);
        this.observer.observe(this.bottomEl);
    }
    onIntersect(entries) {
        return __awaiter(this, void 0, void 0, function* () {
            for (const entry of entries) {
                if (entry.isIntersecting) {
                    if (entry.target === this.topEl) {
                        yield this.dnetobj.invokeMethodAsync(this.callbackTopInView);
                    }
                    if (entry.target === this.bottomEl) {
                        yield this.dnetobj.invokeMethodAsync(this.callbackBottomInView);
                    }
                }
            }
        });
    }
    scrollTo(percentage) {
        let y = (this.scrollEl.scrollHeight - this.scrollEl.clientHeight) * percentage;
        this.scrollEl.scrollTo(0, y);
    }
    dispose() {
        this.observer.disconnect();
        this.observer = null;
    }
}
export function MakeNewHelper(id, scrollEl, topEl, bottomEl, dnetobj, callbackTopInView, callbackBottomInView) {
    var obj = new InfiniteListHelper(scrollEl, topEl, bottomEl, dnetobj, callbackTopInView, callbackBottomInView);
    globalThis[id] = obj;
    return () => {
        obj.dispose();
    };
}
