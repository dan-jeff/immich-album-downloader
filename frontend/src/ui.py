from rich.console import Console
from rich.spinner import Spinner
from rich.table import Table
from rich import box
from rich.panel import Panel
import questionary

console = Console()

def display_main_menu():
    """Displays the main menu and returns the user's choice."""
    console.print(Panel("[bold blue]Immich Photo Downloader & Resizer[/bold blue]", border_style="blue"))
    choice = questionary.select(
        "What would you like to do?",
        choices=[
            "Download photos",
            "Download and resize photos",
            "Resize downloaded photos",
            "Manage resize profiles",
            "Configure Immich connection",
            "Exit"
        ]
    ).ask()
    if choice is None:
        return None
    return choice

def display_spinner(text):
    """Context manager to display a spinner for long-running operations."""
    return console.status(f"[bold green]{text}[/bold green]", spinner="dots")

def display_album_selection(albums):
    """Displays a table of albums and prompts the user to select one."""
    table = Table(
        title=Panel("[bold blue]Available Immich Albums[/bold blue]", border_style="blue"),
        show_header=True,
        header_style="bold green",
        box=box.ROUNDED
    )
    table.add_column("No.", justify="right", style="cyan", no_wrap=True)
    table.add_column("Title", style="magenta", overflow="ellipsis")
    table.add_column("Photos", justify="right", style="green")

    album_map = {}
    local_id_to_immich_id = {}
    for i, album in enumerate(albums):
        local_id = str(i + 1)
        album_id = album['id']
        album_name = album['albumName']
        asset_count = album['assetCount']
        table.add_row(local_id, album_name, str(asset_count))
        album_map[album_id] = album_name
        local_id_to_immich_id[local_id] = album_id

    console.print(table)
    
    choices_list = [f"{i+1}. {album['albumName']}" for i, album in enumerate(albums)]
    choices_list.append("Exit")
    
    selected_album_str = questionary.select(
        "Select an album to download:",
        choices=choices_list
    ).ask()
    if selected_album_str is None:
        return None, None

    if selected_album_str == "Exit":
        return None, None

    selected_local_id = selected_album_str.split('.')[0]
    album_id = local_id_to_immich_id[selected_local_id]
    console.print(f"You selected album: [bold]{album_map[album_id]}[/bold]")
    return album_id, album_map[album_id]

def display_profile_selection(profiles):
    """Displays a table of profiles and prompts the user to select one or more."""
    table = Table(
        title=Panel("[bold blue]Available Resizing Profiles[/bold blue]", border_style="blue"),
        show_header=True,
        header_style="bold green",
        box=box.ROUNDED
    )
    table.add_column("No.", justify="right", style="cyan", no_wrap=True)
    table.add_column("Name", style="magenta", overflow="ellipsis")
    table.add_column("Dimensions", style="green")
    table.add_column("Horizontal", style="green")
    table.add_column("Vertical", style="green")

    profile_map = {}
    local_id_to_profile_name = {}
    for i, profile in enumerate(profiles):
        local_id = str(i + 1)
        profile_name = profile["name"]
        dimensions = f"{profile["width"]}x{profile["height"]}"
        horizontal = "Yes" if profile["include_horizontal"] else "No"
        vertical = "Yes" if profile["include_vertical"] else "No"
        table.add_row(local_id, profile_name, dimensions, horizontal, vertical)
        profile_map[profile_name] = profile
        local_id_to_profile_name[local_id] = profile_name

    console.print(table)

    profile_choices = [f"{i+1}. {profile['name']}" for i, profile in enumerate(profiles)]
    
    selected_profiles_str = questionary.checkbox(
        "Select one or more profiles to use:",
        choices=profile_choices
    ).ask()
    if selected_profiles_str is None:
        return []

    if not selected_profiles_str:
        return []

    selected_profiles = []
    for profile_str in selected_profiles_str:
        local_id = profile_str.split('.')[0]
        if local_id in local_id_to_profile_name:
            profile_name = local_id_to_profile_name[local_id]
            selected_profiles.append(profile_map[profile_name])

    return selected_profiles