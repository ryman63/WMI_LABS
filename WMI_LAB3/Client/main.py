import base64
import requests
import threading
import time
from getpass import getpass

SERVER_URL = "http://localhost:5000/"
update_interval = 10
auth_headers = {}

def main():
    global auth_headers

    username = input("Enter username: ")
    password = getpass("Enter password: ")

    auth_header = base64.b64encode(f"{username}:{password}".encode("utf-8")).decode("utf-8")
    auth_headers = {
        "Authorization": f"Basic {auth_header}"
    }

    print("\nConnected to server.\n")

    threading.Thread(target=periodic_update, daemon=True).start()

    while True:
        print("1. View system stats")
        print("2. View all running processes")
        print("3. Filter processes")
        print("4. Kill process")
        print("5. Change update interval")
        print("0. Exit")
        choice = input("Choose an option: ")

        if choice == "1":
            view_system_stats()
        elif choice == "2":
            view_all_running_processes()
        elif choice == "3":
            filter_processes()
        elif choice == "4":
            kill_process()
        elif choice == "5":
            change_update_interval()
        elif choice == "0":
            print("Exiting...")
            return
        else:
            print("Invalid choice. Try again.")

def periodic_update():
    while True:
        time.sleep(update_interval)
        try:
            response = requests.get(f"{SERVER_URL}system-info", headers=auth_headers)
            response.raise_for_status()
            print("\n[Auto-update] System Info:")
            print(response.text)
            print("")
        except requests.exceptions.RequestException as ex:
            print(f"[Auto-update Error]: {ex}")

        print("1. View system stats")
        print("2. View all running processes")
        print("3. Filter processes")
        print("4. Kill process")
        print("5. Change update interval")
        print("0. Exit")


def view_system_stats():
    try:
        response = requests.get(f"{SERVER_URL}monitor", headers=auth_headers)
        response.raise_for_status()
        print("\nSystem Stats:")
        print(response.text)
        print("")
    except requests.exceptions.RequestException as ex:
        print(f"Error: {ex}")

def view_all_running_processes():
    try:
        response = requests.get(f"{SERVER_URL}processes", headers=auth_headers)
        response.raise_for_status()
        print("\nRunning Processes:")
        print(response.text)
    except requests.exceptions.RequestException as ex:
        print(f"Error: {ex}")

def filter_processes():
    filter_key = input("Enter process name or ID to filter: ")
    try:
        params = {"filter": filter_key}
        response = requests.get(f"{SERVER_URL}processes", headers=auth_headers, params=params)
        response.raise_for_status()
        print("\nFiltered Processes:")
        print(response.text if response.text.strip() else "No processes match the filter.")
    except requests.exceptions.RequestException as ex:
        print(f"Error: {ex}")

def kill_process():
    process_id = input("Enter process ID to kill: ")
    try:
        response = requests.delete(f"{SERVER_URL}process", headers=auth_headers, params={"id": process_id})
        response.raise_for_status()
        print("\nResponse:", response.text)
    except requests.exceptions.RequestException as ex:
        print(f"Error: {ex}")

def change_update_interval():
    global update_interval
    try:
        new_interval = int(input("Enter new update interval (seconds): "))
        if new_interval <= 0:
            raise ValueError("Interval must be greater than 0.")

        response = requests.post(f"{SERVER_URL}update-interval", headers=auth_headers, data=str(new_interval))
        response.raise_for_status()

        update_interval = new_interval
        print("\nUpdate interval changed successfully.")
    except ValueError as ex:
        print(f"Error: {ex}")
    except requests.exceptions.RequestException as ex:
        print(f"Error: {ex}")

if __name__ == "__main__":
    main()
