// this is the scroll effect on the hero page, uses a event listener to detect when the page section we scrolling to is in view
document.addEventListener("DOMContentLoaded", function () {
    var revealSections = document.querySelectorAll("[data-scroll-reveal]");

    if (!revealSections.length || !("IntersectionObserver" in window)) {
        return;
    }
    // this is the observer that will detect when the page section we scrolling to is in view
    var observer = new IntersectionObserver(function (entries) {
        entries.forEach(function (entry) {
            if (!entry.isIntersecting) {
                return;
            }

            entry.target.classList.add("is-inview");
            observer.unobserve(entry.target);
        });
    }, 
    // this is the options for the observer, threshold is the percentage of the section that must be in view to trigger the animation.
    {
        threshold: 0.28,
        rootMargin: "0px 0px -8% 0px"
    });
//loop that will add the class to the section that is in view and unobserve the section
    revealSections.forEach(function (section) {
        section.classList.add("js-reveal");
        observer.observe(section);
    });
});
