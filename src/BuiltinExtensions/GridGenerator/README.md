# Grid Generator Extension

Operates as a "Tool" within the "Tools" UI, lets you generate grids of images to compare prompts or parameters.

- Grids are infinite dimensional, you can add as many axes as you want.
- Grids display as a webpage that you can open and select axis display settings dynamically. You can save an image from the grid view.
- You can opt to generate "Just Images", a "Grid Image", or a "Web Page"
    - "Just Images" as the name implies, just gives you images. They are stored in the normal image path.
    - "Grid Image" gives you one single final image at the end. This currently only works up to 3 axes (X, Y, and Y2), and will fail with more.
    - "Web Page" will generate a special web page with a dynamic advanced grid live-viewer, that lets you reorganize the view freely, and display up to 4 axes at a time (and easily swap to other ones).

## Tricks

- When using numbered parameters, for example `Seed`, you can input `..` between numbers to automatically fill that space, for example `1, 2, .., 10`
    - Must have two numbers before (to identify the start and step), and one number after (to identify the end)
    - For example: `1, 2, .., 5` fills to `1, 2, 3, 4, 5`
    - For example: `1, 3, 5, 6, 6.5, .., 9, 11, 13` fills to `1, 3, 5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 11, 13`
- Any parameters may have `SKIP:` in front of them (all caps!) to skip that value.
    - For example, `1, 2, SKIP: 3, 4` will output a grid that has all of 1,2,3,4, but only has images in 1,2,4.
        - This is useful particularly for when you're reusing grid pages and want to leave a placeholder, or overwrite some images but leave the rest as they were.
