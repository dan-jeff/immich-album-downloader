# Screenshots

This directory contains screenshots used in the main README.md file.

## Required Screenshots

To complete the documentation, please add the following screenshots:

### 1. `dashboard.png`
- Overview of the main dashboard
- Show album grid with thumbnails
- Include navigation sidebar
- Display any active tasks or recent activity

### 2. `albums.png`
- Album browser view
- Show sync status indicators (red border for out-of-sync)
- Include download buttons and asset counts
- Display different album states (synced, out-of-sync, etc.)

### 3. `progress.png`
- Active tasks view with real-time progress
- Show progress bars and status updates
- Include SignalR connectivity indicator
- Display multiple tasks if possible

### 4. `profiles.png`
- Resize profile management interface
- Show profile creation/editing form
- Include orientation filter options
- Display existing profiles in table/card format

### 5. `mobile.png`
- Mobile-responsive interface
- Show card-based layout on mobile
- Include touch-friendly buttons
- Demonstrate responsive navigation

## Guidelines for Screenshots

### Technical Requirements
- **Resolution**: 1920x1080 or higher for desktop views
- **Format**: PNG with transparent background where applicable
- **Mobile**: Use device frame (iPhone/Android) for mobile screenshots
- **Quality**: High DPI, clear text, no compression artifacts

### Content Guidelines
- **Use realistic data**: Don't use placeholder text like "Lorem ipsum"
- **Show features in action**: Demonstrate actual functionality
- **Include relevant UI states**: Loading states, success messages, etc.
- **Highlight key features**: Use arrows or callouts if needed
- **Keep it clean**: Remove any personal information or test data

### Taking Screenshots

1. **Setup test environment** with realistic data
2. **Use browser developer tools** to simulate different screen sizes
3. **Take multiple shots** to capture different states
4. **Edit if necessary** to highlight features or crop appropriately
5. **Optimize file size** while maintaining quality

### Browser Setup
- Use Chrome or Firefox with developer tools
- Clear browser cache and cookies
- Use consistent browser theme (light mode recommended)
- Disable browser extensions that might interfere
- Set zoom level to 100%

### Mobile Screenshots
- Use browser responsive design mode
- Test on actual devices when possible
- Include device frame for context
- Show touch interactions (finger/hand if relevant)

## File Naming Convention

- Use lowercase filenames with hyphens: `album-browser.png`
- Include descriptive names: `resize-profile-creation.png`
- Use consistent naming across similar features
- Avoid abbreviations that might be unclear

## Updating Screenshots

When updating the application:
1. Review existing screenshots for accuracy
2. Update any screenshots that show outdated UI
3. Add new screenshots for new features
4. Remove screenshots for deprecated features
5. Update README.md references as needed

## Tools for Screenshot Creation

### Recommended Tools
- **macOS**: Screenshot (Cmd+Shift+5), CleanShot X
- **Windows**: Snipping Tool, Greenshot, ShareX
- **Linux**: GNOME Screenshot, Flameshot, Shutter
- **Browser**: Built-in screenshot tools, Full Page Screen Capture extensions

### Online Tools
- [Browserframe](https://browserframe.com/) - Add browser frames
- [Device Frames](https://deviceframes.com/) - Add device frames
- [CleanShot Cloud](https://cleanshot.com/) - Annotate and share

## Example File Structure

```
docs/
└── screenshots/
    ├── README.md (this file)
    ├── desktop/
    │   ├── dashboard.png
    │   ├── albums.png
    │   ├── progress.png
    │   └── profiles.png
    ├── mobile/
    │   ├── mobile-dashboard.png
    │   ├── mobile-albums.png
    │   └── mobile-menu.png
    └── features/
        ├── real-time-progress.gif
        ├── album-sync-indicator.png
        └── orientation-filter.png
```

## Contributing Screenshots

When contributing screenshots:
1. Follow the guidelines above
2. Test screenshots in the README context
3. Include multiple device types when relevant
4. Consider creating animated GIFs for interactive features
5. Submit via pull request with description of what's shown