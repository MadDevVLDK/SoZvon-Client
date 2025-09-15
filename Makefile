.PHONY: push commit status pull log help add

DEFAULT_MSG = "not important commit"

help:
	@echo "Available commands:"
	@echo "  make status    - Show git status"
	@echo "  make add       - Stage all changes (git add .)"
	@echo "  make commit    - Commit staged changes (usage: make commit \"Your message\" or just make commit)"
	@echo "  make push      - Push to remote repository (usage: make push")"
	@echo "  make fastpush  - Add, Commit and Push to remote repository (usage: make push \"Your message\" or just make push)"

	@echo "  make pull      - Pull from remote repository"
	@echo "  make log       - Show git log (last 10 commits)"

status: 
	git status

add: 
	git add .

commit:
	@if [ "$(MSG)" ]; then \
		read -p "Enter commit message (default: $(DEFAULT_MESSAGE)): " msg; \
		if [ "$$msg" ]; then \
			echo "Using default message: $(DEFAULT_MESSAGE)"; \
			git commit -m $(DEFAULT_MESSAGE); \
		else \
			echo "Using custom message: $$msg"; \
			git commit -m "$$msg"; \
		fi; \
	else \
		echo "Using custom message: $(MSG)"; \
		git commit -m "$(MSG)"; \
	fi

push:
	git push

fastpush: 
	git add .
	git commit -m DEFAULT_MSG
	git push


pull: 
	git pull

log: 
	git log --oneline -10

# Обработка аргументов без имени
%:
	@: