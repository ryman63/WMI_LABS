import base64
import requests
from getpass import getpass

SERVER_URL = "http://localhost:5000/"
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

    while True:
        print("1. View system stats")
        print("2. View all running processes")
        print("3. Kill process")
        print("0. Exit")
        choice = input("Choose an option: ")

        if choice == "1":
            view_system_stats()
        elif choice == "2":
            view_all_running_processes()
        elif choice == "3":
            kill_process()
        elif choice == "0":
            print("Exiting...")
            return
        else:
            print("Invalid choice. Try again.")

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

def kill_process():
    process_id = input("Enter process ID to kill: ")
    try:
        response = requests.delete(f"{SERVER_URL}process", headers=auth_headers, params={"id": process_id})
        response.raise_for_status()
        print("\nResponse:", response.text)
    except requests.exceptions.RequestException as ex:
        print(f"Error: {ex}")

if __name__ == "__main__":
    main()
