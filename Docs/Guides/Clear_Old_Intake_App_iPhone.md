# Clear the old RV Intake app from an iPhone

> **Draft.** The menu paths here have not yet been confirmed on a real iPhone. Check the
> list at the end before handing this to anyone.

Until October 2026, the intake page could install itself on a phone and keep a copy of
itself. That copy never updated on its own, so a phone that used the old version can keep
showing an out-of-date page. The intake page no longer works that way (#858). This clears
the leftover copy so the phone loads the current page.

Two minutes, once per phone.

**Do this only after the change has been deployed.** If you clear the phone earlier, the next
visit puts the old copy back.

You only need this if the phone opened the intake link **before** the change. A phone that
never opened it has nothing to clear.

---

## 1. Remove the home-screen icon

Skip this step if there is no RV Intake icon on the home screen.

1. Press and hold the **RV Intake** icon.
2. Tap **Remove App**.
3. Tap **Delete from Home Screen**.

## 2. Clear the website data

1. Open **Settings**.
2. Tap **Apps**, then **Safari**. On iOS 17 or earlier, tap **Safari** directly.
3. Scroll to the bottom and tap **Advanced**.
4. Tap **Website Data**.
5. In the search box, type `rvintake`.
6. Swipe left on each result and tap **Delete**. You should see up to two:
   - `rvintake.com`
   - `staging.rvintake.com`
7. Search for `azurestaticapps`. Delete any result the same way.

This removes only those sites' data. It does not sign you out of anything else.

## 3. Check

Open the intake link again. It loads fresh, and the **RV Intake** page shows the current
version.

---

## If the phone uses Chrome

Chrome on iPhone stores its website data in the same place as Safari, so the steps above
clear it too. You do not need to do anything in Chrome's own settings.

## If the icon came back, or the page still looks old

1. Close Safari completely: swipe up from the bottom, pause, then flick Safari away.
2. Repeat step 2.
3. Open the link in a **Private** tab to confirm it shows the current page.

---

## To confirm on a real iPhone

Before this goes to anyone, walk it once on a phone that has the old copy installed:

- [ ] The **Remove App** → **Delete from Home Screen** wording matches what the phone shows.
- [ ] **Settings** → **Apps** → **Safari** → **Advanced** → **Website Data** exists on the
      iOS version in use, and the search box finds `rvintake`.
- [ ] After clearing, the intake link loads the current page with no second reload.
