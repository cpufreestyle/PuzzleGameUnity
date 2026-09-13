"""AltDriver 冒烟测试 - 连接编辑器 Play 模式中的 PuzzleGame (127.0.0.1:13000)"""
from alttester import AltDriver, By


def main():
    driver = AltDriver()  # 默认 127.0.0.1:13000
    try:
        elements = driver.get_all_elements()
        names = [e.name for e in elements]
        print("ELEMENT_COUNT:", len(elements))
        print("SAMPLE_NAMES:", names[:15])

        cam = driver.find_object(By.NAME, "Main Camera")
        print("FIND Main Camera -> name=%s enabled=%s" % (
            cam.name, getattr(cam, "enabled", "?")))

        for target in ("AltTesterPrefab", "EventSystem"):
            print("HAS %s:" % target, target in names)

        try:
            print("CURRENT_SCENE:", driver.get_current_scene())
        except Exception as e:  # 可选 API，不作为冒烟判据
            print("CURRENT_SCENE: skipped (%s)" % e)

        print("SMOKE-PASS")
    finally:
        try:
            driver.stop()
        except Exception:
            pass


if __name__ == "__main__":
    main()
